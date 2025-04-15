using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Media;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Utilities
{
    public class Synchronizer(string syncConnectionString, string mainDatabase, string secondaryDatabase)
    {
        private static readonly string endpoint = "https://testaistorage.search.windows.net";
        private static readonly string apiKey = "<REDACTED>";
        private static readonly string indexName = "sb-final";
        private readonly SynchronizationCache synchronizationCache = new(syncConnectionString, mainDatabase, secondaryDatabase);
        private List<Int32> autoSyncedIds = [];

        public List<List<string>> Synchronize(Filter filter, ItemCache itemCache, DataGrid dataGrid)
        {
            Int32 synchronizedSubjectId = synchronizationCache.GetSynchronizedId(Layer.Subject, filter.GetSubjectId());
            if (synchronizedSubjectId == -1)
                return [];

            autoSyncedIds = [];

            Layer layer = filter.GetLayer();
            if (layer == Layer.KnowledgeType)
                return synchronizationCache.GetSynchronizedId(layer, filter.GetIdByRow(0)) != -1
                       ? [[]]
                       : SynchronizeRow(synchronizedSubjectId, layer, (DataRowView)dataGrid.Items[0], itemCache);

            List<List<string>> result = [];

            for (int i = 0; i < dataGrid.Items.Count; i++)
            {
                if (synchronizationCache.GetSynchronizedId(layer, filter.GetIdByRow(i)) != -1)
                {
                    autoSyncedIds.Add(-1);
                    result.Add([]);
                    continue;
                }

                result.Add(SynchronizeRow(synchronizedSubjectId, layer, (DataRowView)dataGrid.Items[i], itemCache)[0]);
                if (result[^1].IsNullOrEmpty())
                    autoSyncedIds.Add(-1);
            }

            foreach (List<string> row in result)
                if (!row.IsNullOrEmpty())
                    return result;

            return [[]];
        }

        private List<List<string>> SynchronizeRow(Int32 synchronizedSubjectId, Layer layer, DataRowView rowView, ItemCache itemCache)
        {
            string name = rowView[rowView.Row.ItemArray.Length - (layer == Layer.KnowledgeType ? 2 : 1)].ToString() ?? "";

            Int32 secondaryDatabaseId = synchronizationCache.GetSecondaryDatabaseId();

            List<List<string>> choices = [];
            List<Int32> ids = Search(CreateJsonQuery(secondaryDatabaseId, synchronizedSubjectId, layer == Layer.KnowledgeType ? 3 : (Int32)layer, name));
            foreach (Int32 id in ids)
            {
                if (!autoSyncedIds.Contains(id) && synchronizationCache.GetSynchronizedId(layer, id, true) == -1)
                {
                    if (layer != Layer.KnowledgeType)
                        return [UpdateRow(id, layer, itemCache)];

                    List<string> tmp = UpdateRow(id, layer, itemCache);
                    if (!tmp.IsNullOrEmpty())
                        choices.Add(tmp);
                }
            }

            return choices.IsNullOrEmpty() ? [[]] : choices;
        }

        private static string CreateJsonQuery(Int32 databaseId, Int32 subjectId, Int32 itemTypeId, string name)
        {
            name = JsonConvert.SerializeObject(name);
            return $@"
            {{
                ""select"": ""id_item"",
                ""filter"": ""id_database eq {databaseId} and id_subject eq {subjectId} and id_item_type eq {itemTypeId}"",
                ""top"": 10,
                ""vectorQueries"": [
                    {{
                        ""kind"": ""text"",
                        ""text"": {name},
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
            HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult();

            SearchResponse? searchResponse = response.Content.ReadFromJsonAsync<SearchResponse>().GetAwaiter().GetResult();

            if (searchResponse?.Value != null)
                foreach (var item in searchResponse.Value)
                    ids.Add(item.IdItem);

            return ids;
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

        private List<string> UpdateRow(Int32 id, Layer layer, ItemCache itemCache)
        {
            List<string> newRow = new(itemCache.GetItem(layer, id));

            if (newRow.IsNullOrEmpty())
                return [];

            autoSyncedIds.Add(id);
            newRow.Reverse();
            return newRow;
        }

        public void SaveChanges(Filter filter, List<SolidColorBrush> rowColors, int index = -1)
        {
            if (index != -1)
            {
                SavePair(Layer.KnowledgeType, filter.GetIdByRow(0), autoSyncedIds[index]);
                return;
            }

            for (int i = 0; i < autoSyncedIds.Count; i++)
            {
                if (rowColors[i].Equals(ACCEPT_COLOR))
                    SavePair(filter.GetLayer(), filter.GetIdByRow(i), autoSyncedIds[i]);
            }
        }

        public void DeletePair(Layer layer, Int32 id) => synchronizationCache.DeletePair(layer, id);

        public void SavePair(Layer layer, Int32 keyId, Int32 valueId)
        {
            synchronizationCache.SetSynchronizedId(layer, keyId, valueId, true);
            synchronizationCache.SetSynchronizedMirroredId(layer, valueId, keyId);
        }

        public List<Int32> GetSynchronizedIds(Filter filter, bool secondaryDatabaseSearch = false, Int32 id = -1)
        {
            if (id != -1)
                return [synchronizationCache.GetSynchronizedId(filter.GetLayer(), id)];

            return synchronizationCache.GetSynchronizedIds(filter, secondaryDatabaseSearch);
        }

        public void SwitchDatabases() => synchronizationCache.SwitchDatabases();

        public Int32 GetIdByRow(int rowIndex) => autoSyncedIds[rowIndex];
    }
}
