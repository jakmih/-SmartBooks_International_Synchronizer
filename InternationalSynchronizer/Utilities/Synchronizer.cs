using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Utilities
{
    public class Synchronizer(DataManager dataManager)
    {
        private static readonly string _endpoint = "https://testaistorage.search.windows.net";
        private static readonly string _apiKey = AppSettingsLoader.LoadConfiguration().GetSection("Keys")["AISearchKey"]!;
        private static readonly string _indexName = "sb-final";
        private readonly DataManager _dataManager = dataManager;

        public async Task<int> Synchronize(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, Int32 subjectId)
        {
            Int32 synchronizedSubjectId = _dataManager.GetSynchronizedId(Layer.Subject, subjectId);
            if (synchronizedSubjectId == -1)
                return -1;

            if (rightMetadata.GetLayer() == Layer.KnowledgeType)
                return await SynchronizeSpecificKnowledge(leftMetadata, rightMetadata, synchronizedSubjectId);

            SemaphoreSlim semaphore = new(5);
            List<Task<List<(Int32 Id, float Score)>>> matchesTasks = [];

            for (int i = 0; i < leftMetadata.RowCount(); i++)
            {
                if (rightMetadata.GetIdByRow(i) == -1)
                    matchesTasks.Add(GetAIMatchesThrottled(semaphore, synchronizedSubjectId, leftMetadata, i));
                else
                    matchesTasks.Add(Task.FromResult(new List<(Int32, float)>()));
            }

            return await ChooseBestMatchesFirst(rightMetadata, await Task.WhenAll(matchesTasks));
        }

        private async Task<List<(Int32 Id, float Score)>> GetAIMatchesThrottled(SemaphoreSlim semaphore, int subjectId, MyGridMetadata metadata, int rowIndex)
        {
            await semaphore.WaitAsync();
            try
            {
                return await GetAIMatches(subjectId, metadata, rowIndex);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<int> SynchronizeSpecificKnowledge(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, int synchronizedSubjectId)
        {
            if (rightMetadata.GetIdByRow(0) != -1)
                return 0;

            List<(Int32 Id, float Score)> matches = await GetAIMatches(synchronizedSubjectId, leftMetadata, 0);
            if (matches.IsNullOrEmpty())
                return 0;

            var tasks = matches.Select(async match =>
            {
                Int32 id = match.Id;
                if (_dataManager.GetSynchronizedId(Layer.KnowledgeType, id, true) == -1)
                {
                    List<string> newRow = await Task.Run(() => _dataManager.GetItem(rightMetadata.GetLayer(), id));
                    newRow.Reverse();
                    return (id, newRow);
                }
                return (id, (List<string>?)null);
            });

            var results = await Task.WhenAll(tasks);

            rightMetadata.RemoveData();

            foreach (var (id, newRow) in results)
                if (newRow is not null)
                    rightMetadata.AddRow([.. newRow], ACCEPT_COLOR, id);

            return rightMetadata.RowCount();
        }

        private async Task<int> ChooseBestMatchesFirst(MyGridMetadata rightMetadata, List<(int Id, float Score)>[] allRowMatches)
        {
            List<Task> updateRowTasks = [];

            int newRowCount = 0;
            while (true)
            {
                (int chosenRow, int chosenMatch) = await Task.Run(() => ChooseBestMatch(rightMetadata, allRowMatches));
                if (chosenRow == -1)
                    break;

                Int32 id = allRowMatches[chosenRow][chosenMatch].Id;
                await Task.Run(() => rightMetadata.UpdateRowId(chosenRow, id));
                updateRowTasks.Add(UpdateRow(chosenRow, id, rightMetadata));
                allRowMatches[chosenRow] = [];
                newRowCount++;
            }

            await Task.WhenAll(updateRowTasks);

            return newRowCount;
        }

        private (int ChosenRow, int ChosenMatch) ChooseBestMatch(MyGridMetadata rightMetadata, List<(int Id, float Score)>[] allRowMatches)
        {
            float biggestScore = 0;
            int chosenRow = -1;
            int chosenMatch = -1;
            for (int rowIndex = 0; rowIndex < allRowMatches.Length; rowIndex++)
            {
                List<(int Id, float Score)> matches = allRowMatches[rowIndex];
                if (matches.IsNullOrEmpty())
                    continue;

                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    (int Id, float Score) = matches[matchIndex];

                    if (Score <= biggestScore)
                        break;

                    if (!rightMetadata.GetIds().Contains(Id) && _dataManager.GetSynchronizedId(rightMetadata.GetLayer(), Id, true) == -1)
                    {
                        biggestScore = Score;
                        chosenRow = rowIndex;
                        chosenMatch = matchIndex;
                    }
                }
            }
            return (chosenRow, chosenMatch);
        }

        private async Task<List<(Int32 Id, float Score)>> GetAIMatches(Int32 synchronizedSubjectId, MyGridMetadata leftMetadata, int rowIndex)
        {
            Layer layer = leftMetadata.GetLayer();
            Int32 secondaryDatabaseId = _dataManager.GetSecondaryDatabaseId();
            string name = leftMetadata.GetRowData(rowIndex)[^2];
            Int32 synchronizedParentId = _dataManager.GetSynchronizedId(layer - 1, leftMetadata.UpperLayerId);

            string jsonQuery = CreateJsonQuery(databaseId:      secondaryDatabaseId,
                                               subjectId:       synchronizedSubjectId,
                                               itemTypeId:      layer == Layer.KnowledgeType ? 3 : (Int32)layer,
                                               name:            name,
                                               packageId:       layer == Layer.Theme ? synchronizedParentId : -1,
                                               themeId:         layer == Layer.Knowledge || layer == Layer.KnowledgeType ? synchronizedParentId : -1,
                                               knowledgeTypeId: leftMetadata.GetKnowledgeTypeIdByRow(rowIndex));
            
            return await Task.Run(() => Search(jsonQuery));
        }

        private async Task UpdateRow(int rowIndex, Int32 id, MyGridMetadata metadata)
        {
            List<string> newRow = [.. await Task.Run(() => _dataManager.GetItem(metadata.GetLayer(), id))];
            newRow.Reverse();
            metadata.UpdateRow(rowIndex, [.. newRow], ACCEPT_COLOR);
        }

        private static string CreateJsonQuery(Int32 databaseId,
                                              Int32 subjectId,
                                              Int32 itemTypeId,
                                              string name,
                                              Int32 packageId,
                                              Int32 themeId,
                                              Int32 knowledgeTypeId)
        {
            return $@"
            {{
                ""select"": ""id_item"",
                ""filter"": ""id_database eq {databaseId} and id_subject eq {subjectId} and id_item_type eq {itemTypeId}"",
                ""top"": 15,
                ""scoringProfile"": ""priority_search"",
                ""scoringParameters"": [
                    ""packageId:{packageId}"",
                    ""themeId:{themeId}"",
                    ""knowledgeTypeId:{knowledgeTypeId}""
                ],
                ""vectorQueries"": [
                    {{
                        ""kind"": ""text"",
                        ""text"": {JsonConvert.SerializeObject(name)},
                        ""fields"": ""text_vector""
                    }}
                ]
            }}";
        }

        static List<(Int32 Id, float Score)> Search(string jsonQuery)
        {
            List<(Int32, float)> results = [];

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("api-key", _apiKey);

            string url = $"{_endpoint}/indexes/{_indexName}/docs/search?api-version=2024-11-01-preview";
            HttpContent content = new StringContent(jsonQuery, Encoding.UTF8, "application/json");
            Debug.WriteLine(jsonQuery);
            try
            {
                HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult();
                SearchResponse? searchResponse = response.Content.ReadFromJsonAsync<SearchResponse>().GetAwaiter().GetResult();

                if (searchResponse?.Value != null)
                    foreach (var item in searchResponse.Value)
                        results.Add((item.IdItem, item.Score));

                return results;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP request error: {ex.Message}");
                return [];
            }
        }

        public class SearchResponse
        {
            [JsonPropertyName("value")]
            public List<SearchResultItem> Value { get; set; } = [];
        }

        public class SearchResultItem
        {
            [JsonPropertyName("id_item")]
            public Int32 IdItem { get; set; }

            [JsonPropertyName("@search.score")]
            public float Score { get; set; }
        }
    }
}
