using Microsoft.Data.SqlClient;
using static InternationalSynchronizer.Utilities.SqlQuery;

namespace InternationalSynchronizer.Utilities
{
    public class ItemCache
    {
        private Dictionary<Layer, Dictionary<Int32, List<string>>> cache = [];
        private Dictionary<Layer, Dictionary<Int32, List<string>>> switchCache = [];
        private string connectionString;
        private string switchConnectionString;


        public ItemCache(string mainConnectionString, string secondaryConnectionString)
        {
            cache.Add(Layer.Subject, []);
            cache.Add(Layer.Package, []);
            cache.Add(Layer.Theme, []);
            cache.Add(Layer.Knowledge, []);
            cache.Add(Layer.KnowledgeType, []);
            switchCache.Add(Layer.Subject, []);
            switchCache.Add(Layer.Package, []);
            switchCache.Add(Layer.Theme, []);
            switchCache.Add(Layer.Knowledge, []);
            switchCache.Add(Layer.KnowledgeType, []);
            connectionString = secondaryConnectionString;
            switchConnectionString = mainConnectionString;
        }

        public List<string> GetItem(Layer layer, Int32 id)
        {
            return cache[layer].TryGetValue(id, out List<string>? value) ? value : LoadItem(layer, id);
        }

        private List<string> LoadItem(Layer layer, Int32 id)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand(GetItemQuery(layer, id, false), connection);
            using var reader = command.ExecuteReader();

            List<string> item = [];
            if (reader.Read())
                for (int i = 0; i < reader.FieldCount - 1; i++)
                    item.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            reader.Close();
            cache[layer].Add(id, item);
            return item;
        }

        public void Switch()
        {
            (cache, switchCache) = (switchCache, cache);
            (connectionString, switchConnectionString) = (switchConnectionString, connectionString);
        }
    }
}
