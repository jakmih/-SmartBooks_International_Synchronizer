using Microsoft.Data.SqlClient;
using static InternationalSynchronizer.Utilities.SqlQuery;

namespace InternationalSynchronizer.Utilities
{
    public class SynchronizationCache
    {
        private readonly string synchronizationConnectionString;
        private Dictionary<Layer, Dictionary<Int32, Int32>> cache = [];
        private Dictionary<Layer, Dictionary<Int32, Int32>> switchCache = [];
        private Int32 mainDatabaseId;
        private Int32 secondaryDatabaseId;

        public SynchronizationCache(string syncConnectionString, string mainDatabase, string secondaryDatabase)
        {
            cache.Add(Layer.Subject, []);
            cache.Add(Layer.Package, []);
            cache.Add(Layer.Theme, []);
            cache.Add(Layer.Knowledge, []);
            switchCache.Add(Layer.Subject, []);
            switchCache.Add(Layer.Package, []);
            switchCache.Add(Layer.Theme, []);
            switchCache.Add(Layer.Knowledge, []);
            synchronizationConnectionString = syncConnectionString;
            mainDatabaseId = GetDatabaseId(mainDatabase);
            secondaryDatabaseId = GetDatabaseId(secondaryDatabase);
        }

        private Int32 GetDatabaseId(string database)
        {
            using var connection = new SqlConnection(synchronizationConnectionString);
            connection.Open();
            using var command = new SqlCommand(GetDatabaseIdQuery(database), connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
                return reader.GetInt32(0);

            reader.Close();
            return CreateDatabase(database, connection);
        }

        private static Int32 CreateDatabase(string database, SqlConnection connection)
        {
            using var command = new SqlCommand(GetInsertDatabaseQuery(database), connection);
            command.ExecuteNonQuery();

            using var idCommand = new SqlCommand("SELECT SCOPE_IDENTITY()", connection);
            return Convert.ToInt32(idCommand.ExecuteScalar());
        }

        public List<Int32> GetSynchronizedIds(Filter filter, bool secondaryDatabaseSearch)
        {
            List<Int32> synchronizedIds = [];
            using var connection = new SqlConnection(synchronizationConnectionString);
            connection.Open();

            foreach (Int32 id in filter.GetIds())
                synchronizedIds.Add(GetSynchronizedId(filter.GetLayer(), id, secondaryDatabaseSearch, connection));

            return synchronizedIds;
        }

        public Int32 GetSynchronizedId(Layer layer, Int32 id, bool secondaryDatabaseSearch = false, SqlConnection? connection = null)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (secondaryDatabaseSearch)
                return switchCache[layer].TryGetValue(id, out Int32 value) ? value : LoadSynchronizedId(layer, id, true, connection);
            else
                return cache[layer].TryGetValue(id, out Int32 value) ? value : LoadSynchronizedId(layer, id, false, connection);
        }

        private Int32 LoadSynchronizedId(Layer layer, Int32 id, bool secondaryDatabaseSearch, SqlConnection? connection)
        {
            string query;
            if (secondaryDatabaseSearch)
                query = GetSyncPairQuery((Int32)layer, id, secondaryDatabaseId, mainDatabaseId);
            else
                query = GetSyncPairQuery((Int32)layer, id, mainDatabaseId, secondaryDatabaseId);

            if (connection == null)
            {
                connection = new SqlConnection(synchronizationConnectionString);
                connection.Open();
            }
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                Int32 keyId = reader.GetInt32(0) == id ? reader.GetInt32(0) : reader.GetInt32(1);
                Int32 valueId = reader.GetInt32(1) == id ? reader.GetInt32(0) : reader.GetInt32(1);
                SetSynchronizedId(layer, keyId, valueId);
                SetSynchronizedMirroredId(layer, valueId, keyId);
                reader.Close();
                return valueId;
            }

            if (secondaryDatabaseSearch)
                SetSynchronizedMirroredId(layer, id, -1);
            else
                SetSynchronizedId(layer, id, -1);

            return -1;
        }

        public void SetSynchronizedId(Layer layer, Int32 keyId, Int32 valueId, bool addToDatabase = false)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (!cache[layer].TryAdd(keyId, valueId))
                cache[layer][keyId] = valueId;

            if (!addToDatabase || keyId == -1 || valueId == -1)
                return;

            using var connection = new SqlConnection(synchronizationConnectionString);
            connection.Open();

            Int32 syncItemId1 = GetOrCreateSyncItem(connection, keyId, layer, mainDatabaseId);
            Int32 syncItemId2 = GetOrCreateSyncItem(connection, valueId, layer, secondaryDatabaseId);

            try
            {
                using var command = new SqlCommand(GetInsertSyncPairQuery(syncItemId1, syncItemId2), connection);
                int tmp = command.ExecuteNonQuery();
            }
            catch (SqlException e)
            {
                if (!(e.Message.Contains("PRIMARY KEY constraint") ||
                    e.Message.Contains("FOREIGN KEY constraint") ||
                    e.Message.Contains("Violation of UNIQUE KEY constraint")))
                    throw;
            }
        }

        public void SetSynchronizedMirroredId(Layer layer, Int32 keyId, Int32 valueId)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (!switchCache[layer].TryAdd(keyId, valueId))
                switchCache[layer][keyId] = valueId;
        }

        private static Int32 GetOrCreateSyncItem(SqlConnection connection, Int32 id, Layer layer, Int32 databaseId)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            string query = GetSyncItemQuery((Int32)layer, id, databaseId);
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                int existingId = reader.GetInt32(0);
                reader.Close();
                return existingId;
            }

            reader.Close();
            query = GetInsertSyncItemQuery((Int32)layer, id, databaseId);
            using var insertCommand = new SqlCommand(query, connection);
            insertCommand.ExecuteNonQuery();

            using var idCommand = new SqlCommand("SELECT SCOPE_IDENTITY()", connection);
            return Convert.ToInt32(idCommand.ExecuteScalar());
        }

        public void DeletePair(Layer layer, Int32 id)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            Int32 pairItemId;
            if ((pairItemId = GetSynchronizedId(layer, id)) == -1)
                return;

            switchCache[layer].Remove(cache[layer].GetValueOrDefault(id));
            cache[layer].Remove(id);

            using var connection = new SqlConnection(synchronizationConnectionString);
            connection.Open();

            Int32 id1 = GetOrCreateSyncItem(connection, id, layer, mainDatabaseId);
            Int32 id2 = GetOrCreateSyncItem(connection, pairItemId, layer, secondaryDatabaseId);

            using var command = new SqlCommand(GetDeleteSyncPairQuery(id1, id2), connection);
            command.ExecuteNonQuery();
        }

        public void SwitchDatabases()
        {
            (cache, switchCache) = (switchCache, cache);
            (mainDatabaseId, secondaryDatabaseId) = (secondaryDatabaseId, mainDatabaseId);
        }

        public Int32 GetSecondaryDatabaseId() => secondaryDatabaseId;
    }
}
