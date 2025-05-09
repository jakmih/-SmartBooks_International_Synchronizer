using Microsoft.Data.SqlClient;
using System.Data;
using static InternationalSynchronizer.Utilities.SqlQuery;
using static InternationalSynchronizer.Utilities.AppColors;
using Microsoft.Extensions.Configuration;

namespace InternationalSynchronizer.Utilities
{
    public class SynchronizationCache
    {
        private readonly string _connectionString;
        private readonly Dictionary<Layer, Dictionary<Int32, Int32>> _cache = [];
        private readonly Dictionary<Layer, Dictionary<Int32, Int32>> _mirrorCache = [];
        private readonly Int32 _mainDatabaseId;
        private readonly Int32 _secondaryDatabaseId;

        public SynchronizationCache(Int32 mainDatabaseId, Int32 secondaryDatabaseId)
        {
            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
            {
                _cache.Add(layer, []);
                _mirrorCache.Add(layer, []);
            }
            _connectionString = App.Config.GetConnectionString("Sync")!;
            _mainDatabaseId = mainDatabaseId;
            _secondaryDatabaseId = secondaryDatabaseId;
        }

        public List<Int32> GetSynchronizedIds(Filter filter, bool secondaryDatabaseSearch)
        {
            List<Int32> synchronizedIds = [];
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (Int32 id in filter.GetIds())
                synchronizedIds.Add(GetSynchronizedId(filter.Layer, id, secondaryDatabaseSearch, connection));

            return synchronizedIds;
        }

        public Int32 GetSynchronizedId(Layer layer, Int32 id, bool secondaryDatabaseSearch = false, SqlConnection? connection = null)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (secondaryDatabaseSearch)
                return _mirrorCache[layer].TryGetValue(id, out Int32 value) ? value : LoadSynchronizedId(layer, id, true, connection);
            else
                return _cache[layer].TryGetValue(id, out Int32 value) ? value : LoadSynchronizedId(layer, id, false, connection);
        }

        private Int32 LoadSynchronizedId(Layer layer, Int32 id, bool secondaryDatabaseSearch, SqlConnection? connection)
        {
            string query = secondaryDatabaseSearch
                         ? SyncPairQuery((Int32)layer, id, _secondaryDatabaseId, _mainDatabaseId)
                         : SyncPairQuery((Int32)layer, id, _mainDatabaseId, _secondaryDatabaseId);

            if (connection != null)
            {
                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                return ReadAndSetSyncIds(layer, id, reader, secondaryDatabaseSearch);
            }
            else
            {
                using var newConnection = new SqlConnection(_connectionString);
                newConnection.Open();

                using var command = new SqlCommand(query, newConnection);
                using var reader = command.ExecuteReader();

                return ReadAndSetSyncIds(layer, id, reader, secondaryDatabaseSearch);
            }
        }

        private Int32 ReadAndSetSyncIds(Layer layer, Int32 id, SqlDataReader reader, bool secondaryDatabaseSearch)
        {
            if (reader.Read())
            {
                Int32 keyId = reader.GetInt32(0) == id ? reader.GetInt32(0) : reader.GetInt32(1);
                Int32 valueId = reader.GetInt32(1) == id ? reader.GetInt32(0) : reader.GetInt32(1);

                SetSynchronizedId(layer, keyId, valueId);
                SetSynchronizedMirroredId(layer, valueId, keyId);
                return valueId;
            }

            if (secondaryDatabaseSearch)
                SetSynchronizedMirroredId(layer, id, -1);
            else
                SetSynchronizedId(layer, id, -1);

            return -1;
        }

        public int SaveAll(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata)
        {
            var pairsToInsert = new DataTable();
            pairsToInsert.Columns.Add("id_sync_item_1", typeof(int));
            pairsToInsert.Columns.Add("id_sync_item_2", typeof(int));

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            for (int i = 0; i < leftMetadata.RowCount(); i++)
            {
                if (rightMetadata.IsRowColor(i, ACCEPT_COLOR))
                {
                    int leftId = leftMetadata.GetIdByRow(i);
                    int rightId = rightMetadata.GetIdByRow(i);

                    if (leftId == -1 || rightId == -1)
                        continue;

                    Layer layer = leftMetadata.GetLayer() == Layer.KnowledgeType ? Layer.Knowledge : leftMetadata.GetLayer();
                    if (!_cache[layer].TryAdd(leftId, rightId))
                        _cache[layer][leftId] = rightId;

                    SetSynchronizedMirroredId(layer, rightId, leftId);

                    int leftSyncItemId = GetOrCreateSyncItem(connection, leftId, layer, _mainDatabaseId);
                    int rightSyncItemId = GetOrCreateSyncItem(connection, rightId, layer, _secondaryDatabaseId);

                    pairsToInsert.Rows.Add(leftSyncItemId, rightSyncItemId);
                }
            }

            using var bulkCopy = new SqlBulkCopy(_connectionString, SqlBulkCopyOptions.KeepIdentity) { DestinationTableName = "sync_pair" };
            bulkCopy.ColumnMappings.Add("id_sync_item_1", "id_sync_item_1");
            bulkCopy.ColumnMappings.Add("id_sync_item_2", "id_sync_item_2");

            bulkCopy.WriteToServer(pairsToInsert);

            return pairsToInsert.Rows.Count;
        }

        public void SetSynchronizedId(Layer layer, Int32 keyId, Int32 valueId, bool addToDatabase = false)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (!_cache[layer].TryAdd(keyId, valueId))
                _cache[layer][keyId] = valueId;

            if (!addToDatabase || keyId == -1 || valueId == -1)
                return;

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            Int32 syncItemId1 = GetOrCreateSyncItem(connection, keyId, layer, _mainDatabaseId);
            Int32 syncItemId2 = GetOrCreateSyncItem(connection, valueId, layer, _secondaryDatabaseId);

            try
            {
                using var command = new SqlCommand(InsertSyncPairQuery(syncItemId1, syncItemId2), connection);
                int tmp = command.ExecuteNonQuery();
            }
            catch (SqlException)
            {
                _cache[layer][keyId] = -1;
                throw;
            }
        }

        public void SetSynchronizedMirroredId(Layer layer, Int32 keyId, Int32 valueId)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            if (!_mirrorCache[layer].TryAdd(keyId, valueId))
                _mirrorCache[layer][keyId] = valueId;
        }

        private static Int32 GetOrCreateSyncItem(SqlConnection connection, Int32 id, Layer layer, Int32 databaseId)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            string query = SyncItemQuery((Int32)layer, id, databaseId);
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                int existingId = reader.GetInt32(0);
                reader.Close();
                return existingId;
            }

            reader.Close();
            query = InsertSyncItemQuery((Int32)layer, id, databaseId);
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

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            Int32 id1 = GetOrCreateSyncItem(connection, id, layer, _mainDatabaseId);
            Int32 id2 = GetOrCreateSyncItem(connection, pairItemId, layer, _secondaryDatabaseId);

            using var command = new SqlCommand(DeleteSyncPairQuery(id1, id2), connection);
            command.ExecuteNonQuery();

            _mirrorCache[layer].Remove(_cache[layer].GetValueOrDefault(id));
            _cache[layer].Remove(id);
        }

        public Int32 GetSecondaryDatabaseId() => _secondaryDatabaseId;
    }
}
