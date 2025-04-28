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
        private static readonly string endpoint = "https://testaistorage.search.windows.net";
        private static readonly string apiKey = AppSettingsLoader.LoadConfiguration().GetSection("Keys")["AISearchKey"]!;
        private static readonly string indexName = "sb-final";
        private readonly DataManager dataManager = dataManager;

        public int Synchronize(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, Int32 subjectId)
        {
            Int32 synchronizedSubjectId = dataManager.GetSynchronizedId(Layer.Subject, subjectId);
            if (synchronizedSubjectId == -1)
                return -1;

            Layer layer = leftMetadata.GetLayer();
            MyGridMetadata newMetadata = new(layer, true);

            if (layer == Layer.KnowledgeType)
            {
                if (rightMetadata.GetIdByRow(0) != -1)
                    return 0;

                int tmp = SynchronizeRow(synchronizedSubjectId, newMetadata, leftMetadata.GetRowData(0)[^2]);
                if (tmp != 0)
                    rightMetadata.CopyMetadata(newMetadata);

                return tmp;
            }
            int newRowCount = 0;

            for (int i = 0; i < leftMetadata.RowCount(); i++){
                if (rightMetadata.GetIdByRow(i) == -1)
                    newRowCount += SynchronizeRow(synchronizedSubjectId, newMetadata, leftMetadata.GetRowData(i)[^2]);
                else
                    newMetadata.AddRow(rightMetadata.GetRowData(i), rightMetadata.GetRowColor(i), rightMetadata.GetIdByRow(i));
}
            if (newRowCount != 0)
                rightMetadata.CopyMetadata(newMetadata);

            return newRowCount;
        }

        private int SynchronizeRow(Int32 synchronizedSubjectId, MyGridMetadata newMetadata, string name)
        {
            Layer layer = newMetadata.GetLayer();

            Int32 secondaryDatabaseId = dataManager.GetSecondaryDatabaseId();

            int choiceCount = 0;
            List<Int32> ids = Search(CreateJsonQuery(secondaryDatabaseId, synchronizedSubjectId, layer == Layer.KnowledgeType ? 3 : (Int32)layer, name));

            foreach (Int32 id in ids)
                if (!newMetadata.GetIds().Contains(id) && dataManager.GetSynchronizedId(layer, id, true) == -1)
                {
                    if (layer == Layer.KnowledgeType)
                        choiceCount += UpdateRow(id, newMetadata);
                    else if (UpdateRow(id, newMetadata) != 0)
                        return 1;
                }

            if (layer != Layer.KnowledgeType)
                newMetadata.AddRow([], NEUTRAL_COLOR, -1);

            return choiceCount;
        }

        private int UpdateRow(Int32 id, MyGridMetadata newMetadata)
        {
            List<string> newRow = [.. dataManager.GetItem(newMetadata.GetLayer(), id)];
            if (newRow.IsNullOrEmpty())
                return 0;

            newRow.Reverse();
            newMetadata.AddRow([.. newRow], ACCEPT_COLOR, id);
            return 1;
        }

        private static string CreateJsonQuery(Int32 databaseId, Int32 subjectId, Int32 itemTypeId, string name)
        {
            return $@"
            {{
                ""select"": ""id_item"",
                ""filter"": ""id_database eq {databaseId} and id_subject eq {subjectId} and id_item_type eq {itemTypeId}"",
                ""top"": 10,
                ""vectorQueries"": [
                    {{
                        ""kind"": ""text"",
                        ""text"": {JsonConvert.SerializeObject(name)},
                        ""fields"": ""text_vector""
                    }}
                ]
            }}";
        }

        static List<Int32> Search(string jsonQuery)
        {
            List<Int32> ids = [];

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            string url = $"{endpoint}/indexes/{indexName}/docs/search?api-version=2024-11-01-preview";
            HttpContent content = new StringContent(jsonQuery, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult();
                SearchResponse? searchResponse = response.Content.ReadFromJsonAsync<SearchResponse>().GetAwaiter().GetResult();
                if (searchResponse?.Value != null)
                    foreach (var item in searchResponse.Value)
                        ids.Add(item.IdItem);

                return ids;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP request error: {ex.Message}");
            }
            return [];
        }

        public class SearchResponse
        {
            [JsonPropertyName("value")]
            public List<SearchResultItem> Value { get; set; } = [];
        }

        public class SearchResultItem
        {
            [JsonPropertyName("id_item")]
            public int IdItem { get; set; }
        }
    }
}
