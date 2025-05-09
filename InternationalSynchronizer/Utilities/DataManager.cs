using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using static InternationalSynchronizer.Utilities.SqlQuery;
using System.Diagnostics;

namespace InternationalSynchronizer.Utilities
{
    public class DataManager
    {
        private readonly Int32 _mainDatabaseId;
        private readonly Int32 _secondaryDatabaseId;
        private readonly string _connectionString;
        private readonly Synchronizer _synchronizer;
        private readonly SynchronizationCache _synchronizationCache;
        private readonly Dictionary<Layer, Dictionary<Int32, List<string>>> _itemCache = [];

        public DataManager(Int32 mainDatabaseId, Int32 secondaryDatabaseId)
        {
            _mainDatabaseId = mainDatabaseId;
            _secondaryDatabaseId = secondaryDatabaseId;
            _connectionString = App.Config.GetConnectionString("Sync")!;
            _synchronizer = new(this);
            _synchronizationCache = new(mainDatabaseId, secondaryDatabaseId);
            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
                _itemCache.Add(layer, []);
        }

        public FullData GetFilterData(Filter filter, Mode mode)
        {
            List<string> filterData = [];
            MyGridMetadata leftMetadata = new(filter.Layer, filter.GetUpperLayerId());
            MyGridMetadata rightMetadata = new(filter.Layer, -1, true, mode == Mode.ManualSync);

            if (mode != Mode.ManualSync)
            {
                SetDataTables(leftMetadata, rightMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(leftMetadata.GetIds());
            }
            else
            {
                SetDataTables(rightMetadata, leftMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(rightMetadata.GetIds());
            }

            return new FullData(leftMetadata, rightMetadata, filterData, filter.Layer);
        }

        private void SetDataTables(MyGridMetadata mainMetadata, MyGridMetadata syncMetadata, List<string> filterData, Int32 selectedItemId)
        {
            Layer layer = mainMetadata.GetLayer();

            string connectionString = _connectionString;
            string query = DataQuery(layer,
                                     selectedItemId,
                                     mainMetadata.IsRightSide() ? _secondaryDatabaseId : _mainDatabaseId,
                                     mainMetadata.IsRightSide() ? _mainDatabaseId : _secondaryDatabaseId);
            Debug.WriteLine(query);
            Debug.WriteLine(selectedItemId);
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 60;
            using var reader = command.ExecuteReader();

            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            int row = 0;

            while (reader.Read())
            {
                mainMetadata.ExtractRowData(reader, true);
                syncMetadata.ExtractRowData(reader, false);

                filterData.Add(mainMetadata.GetRowData(row++)[mainMetadata.IsRightSide() ? 1 : ^2]);
            }
        }

        public int Synchronize(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, Int32 subjectId) => _synchronizer.Synchronize(leftMetadata, rightMetadata, subjectId).Result;

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

        public void DeletePair(Layer layer, Int32 id) => _synchronizationCache.DeletePair(layer, id);

        public Int32 GetSynchronizedId(Layer layer, Int32 itemId, bool secondaryDatabaseSearch = false) => _synchronizationCache.GetSynchronizedId(layer, itemId, secondaryDatabaseSearch);

        public Int32 GetSecondaryDatabaseId() => _synchronizationCache.GetSecondaryDatabaseId();

        public List<string> GetItem(Layer layer, Int32 id)
        {
            if (_itemCache[layer].TryGetValue(id, out List<string>? value))
                return value;

            List<string> item = LoadItem(layer, id);
            _itemCache[layer][id] = item;
            return item;
        }

        public List<string> LoadItem(Layer layer, Int32 id)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(OneItemQuery(layer, id, _secondaryDatabaseId), connection);
            using var reader = command.ExecuteReader();

            List<string> item = [];
            if (reader.Read())
                for (int i = 0; i < reader.FieldCount; i++)
                    item.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            return item;
        }
    }
}
