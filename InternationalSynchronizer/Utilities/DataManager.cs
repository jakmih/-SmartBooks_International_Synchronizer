using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using static InternationalSynchronizer.Utilities.SqlQuery;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Utilities
{
    public class DataManager
    {
        private readonly string _mainConnectionString;
        private readonly string _secondaryConnectionString;
        private readonly ItemCache _itemCache;
        private readonly SynchronizationCache _synchronizationCache;
        private readonly Synchronizer _synchronizer;

        private DataManager(string mainDatabase, string secondaryDatabase)
        {
            IConfiguration config = AppSettingsLoader.LoadConfiguration();
            _mainConnectionString = config.GetConnectionString(mainDatabase)!;
            _secondaryConnectionString = config.GetConnectionString(secondaryDatabase)!;
            _itemCache = new(this);
            _synchronizationCache = new(config.GetConnectionString("Sync")!);
            _synchronizer = new(this);
        }

        public static async Task<DataManager> CreateAsync(string mainDatabase, string secondaryDatabase)
        {
            var manager = new DataManager(mainDatabase, secondaryDatabase);
            await manager._synchronizationCache.InitializeAsync(mainDatabase, secondaryDatabase);
            return manager;
        }

        public FullData GetFilterData(Filter filter, Mode mode)
        {
            List<string> filterData = [];
            MyGridMetadata leftMetadata = new(filter.Layer, filter.GetUpperLayerId());
            MyGridMetadata rightMetadata = new(filter.Layer, -1, true);

            if (mode != Mode.ManualSync)
            {
                SetDataTable(leftMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(leftMetadata.GetIds());
                SetSynchonizedDataTable(filter, rightMetadata, filter.GetUpperLayerId());
            }
            else
            {
                SetDataTable(rightMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(rightMetadata.GetIds());
                VisualiseSyncedItems(rightMetadata, filter);
            }

            return new FullData(leftMetadata, rightMetadata, filterData, filter.Layer);
        }

        private void SetDataTable(MyGridMetadata metadata, List<string> filterData, Int32 selectedItemId)
        {
            Layer layer = metadata.GetLayer();

            string connectionString = metadata.IsRightSide() ? _secondaryConnectionString : _mainConnectionString;
            string query = ItemQuery(layer, selectedItemId, true);

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            var rows = new List<(Int32, List<string>, Int32)>();

            while (reader.Read())
            {
                Int32 id = reader.GetInt32(reader.FieldCount - 1);
                List<string> row = ExtractRowData(reader, metadata.IsRightSide());
                Int32 knowledgeTypeId = (layer == Layer.Knowledge || layer == Layer.KnowledgeType)
                                      ? reader.GetInt32(reader.FieldCount - 2)
                                      : -1;

                rows.Add((id, row, knowledgeTypeId));
            }
            reader.Close();

            foreach (var (id, row, knowledgeTypeId) in rows)
            {
                if (!metadata.IsRightSide() && layer != Layer.Knowledge && layer != Layer.KnowledgeType)
                {
                    string syncedChildCount = _itemCache.GetSyncedChildCount(layer, id);
                    if (syncedChildCount == "" || syncedChildCount[^1] == '?')
                    {
                        _itemCache.AddChildCount(layer, id, (-1, TotalChildCount(layer, id, connection)));
                        syncedChildCount = _itemCache.GetSyncedChildCount(layer, id);
                    }

                    row.Add(syncedChildCount);
                }

                filterData.Add(metadata.IsRightSide() ? row[0] : row[^2]);
                metadata.AddRow([.. row], NEUTRAL_COLOR, id, knowledgeTypeId);
            }
        }

        private static List<string> ExtractRowData(SqlDataReader reader, bool mirrorRowData)
        {
            List<string> rowData = [];
            for (int i = 0; i < reader.FieldCount - 2; i++)
                rowData.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            if (mirrorRowData)
                rowData.Reverse();

            return rowData;
        }

        private static int TotalChildCount(Layer layer, int id, SqlConnection connection)
        {
            string query = ItemQuery(layer + 1, id, true, true);

            using var command = new SqlCommand(query, connection);
            object? result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return -1;

            return Convert.ToInt32(result);
        }

        private void SetSynchonizedDataTable(Filter filter, MyGridMetadata metadata, Int32 selectedItemId)
        {
            int syncedItemCount = 0;

            foreach (Int32 id in _synchronizationCache.GetSynchronizedIds(filter, false))
            {
                syncedItemCount++;
                if (id == -1)
                {
                    metadata.AddRow([], NEUTRAL_COLOR, id);
                    syncedItemCount--;
                }
                else if (TryGetRightRow(out List<string> rightRow, metadata.GetLayer(), id)){
                    metadata.AddRow([.. rightRow], NEUTRAL_COLOR, id);}
                else
                    metadata.AddRow(["POLOŽKA BOLA ODSTRÁNENÁ - ID: " + id], DELETED_ITEM_COLOR, id);
            }

            _itemCache.AddChildCount(metadata.GetLayer() - 1, selectedItemId, (syncedItemCount, -1));
        }

        private bool TryGetRightRow(out List<string> row, Layer layer, int id)
        {
            row = [.. _itemCache.GetItem(layer, id)];

            if (row.IsNullOrEmpty())
                return false;

            row.Reverse();
            return true;
        }

        public void VisualiseSyncedItems(MyGridMetadata metadata, Filter filter)
        {
            int index = 0;
            foreach (Int32 id in _synchronizationCache.GetSynchronizedIds(filter, metadata.IsRightSide()))
            {
                if (id != -1)
                    metadata.SetRowColor(index, SYNCED_COLOR);
                index++;
            }
        }

        public int Synchronize(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, Int32 subjectId) => _synchronizer.Synchronize(leftMetadata, rightMetadata, subjectId);

        public int SaveAISyncChanges(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, int index = -1)
        {
            if (index != -1)
                return SavePair(Layer.KnowledgeType, leftMetadata.GetIdByRow(0), rightMetadata.GetIdByRow(index));
            else
                return _synchronizationCache.SaveAll(leftMetadata, rightMetadata);
        }

        public int SavePair(Layer layer, Int32 leftId, Int32 rightId)
        {
            _synchronizationCache.SetSynchronizedId(layer, leftId, rightId, true);
            _synchronizationCache.SetSynchronizedMirroredId(layer, rightId, leftId);
            return 1;
        }

        public int DeletePair(Layer layer, Int32 id)
        {
            _synchronizationCache.DeletePair(layer, id);
            return -1;
        }

        public Int32 GetSynchronizedId(Layer layer, Int32 itemId, bool secondaryDatabaseSearch = false) => _synchronizationCache.GetSynchronizedId(layer, itemId, secondaryDatabaseSearch);

        public Int32 GetSecondaryDatabaseId() => _synchronizationCache.GetSecondaryDatabaseId();

        public void AddToSyncedChildCount(Layer layer, Int32 id, int syncedChildCount) => _itemCache.AddToSyncedChildCount(layer, id, syncedChildCount);

        public List<string> GetItem(Layer layer, Int32 id) => _itemCache.GetItem(layer, id);

        public List<string> LoadItem(Layer layer, Int32 id)
        {
            using var connection = new SqlConnection(_secondaryConnectionString);
            connection.Open();

            using var command = new SqlCommand(ItemQuery(layer, id), connection);
            using var reader = command.ExecuteReader();

            List<string> item = [];
            if (reader.Read())
                for (int i = 0; i < reader.FieldCount - 2; i++)
                    item.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            return item;
        }
    }
}
