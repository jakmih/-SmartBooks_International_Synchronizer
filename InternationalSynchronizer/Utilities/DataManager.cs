using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using static InternationalSynchronizer.Utilities.SqlQuery;

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
            FullData fullData = new (new(filter.Layer, filter.GetUpperLayerId(), mode == Mode.ManualSync),
                                     new(filter.Layer, -1, mode != Mode.ManualSync, false),
                                     [],
                                     filter.Layer);

            if (mode != Mode.ManualSync)
                SetDataTables(fullData, filter.GetUpperLayerId());
            else
                SetDataTables(fullData, filter.GetUpperLayerId());

            filter.SetIds(fullData.MainMetadata.GetIds());

            return fullData;
        }

        private void SetDataTables(FullData fullData, Int32 selectedItemId)
        {
            string connectionString = _connectionString;
            string query = DataQuery(fullData.Layer,
                                     selectedItemId,
                                     fullData.MainMetadata.IsRightSide() ? _secondaryDatabaseId : _mainDatabaseId,
                                     fullData.MainMetadata.IsRightSide() ? _mainDatabaseId : _secondaryDatabaseId);

            Debug.WriteLine($"Query: {query}");
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 100;
            using var reader = command.ExecuteReader();
            SetRowOrder(fullData, reader);
        }

        private static void SetRowOrder(FullData fullData, SqlDataReader reader)
        {
            List<(List<string>, List<string>)> syncedRows = [];
            List<(List<string>, List<string>)> notSyncedRows = [];

            while (reader.Read())
            {
                (List<string>, List<string>) row = (ExtractMainRowData(reader, fullData.Layer), ExtractSyncRowData(reader, fullData.Layer));

                if (reader.GetInt32(reader.GetOrdinal("PairedItemId")) == -1)
                    notSyncedRows.Add(row);
                else
                    syncedRows.Add(row);
            }

            List<(List<string>, List<string>)> allRows = [];
            allRows.AddRange(syncedRows);
            allRows.AddRange(notSyncedRows);

            for (int row = 0; row < allRows.Count; row++)
            {
                fullData.MainMetadata.AddRow(allRows[row].Item1, true);
                fullData.SyncMetadata.AddRow(allRows[row].Item2, false);

                fullData.FilterData.Add(fullData.MainMetadata.GetRowData(row)[fullData.MainMetadata.IsRightSide() ? 1 : ^2]);
            }
        }

        private static List<string> ExtractMainRowData(SqlDataReader reader, Layer layer)
        {
            List<string> rowData = [GetFromReader(reader, "Subject")];

            if (layer != Layer.Subject)
            {
                rowData.Add(GetFromReader(reader, "Package"));
                if (layer != Layer.Package)
                {
                    rowData.Add(GetFromReader(reader, "Theme"));
                    if (layer != Layer.Theme)
                    {
                        rowData.Add(GetFromReader(reader, "ThemePart"));
                        rowData.Add(GetFromReader(reader, "Knowledge"));
                        rowData.Add(GetFromReader(reader, "KnowledgeType"));
                    }
                }
            }
            if (layer != Layer.Knowledge && layer != Layer.SpecificKnowledge)
                rowData.Add(GetFromReader(reader, "SyncedChildrenRatio"));

            rowData.Add(reader.GetInt32(reader.GetOrdinal("Id")).ToString());
            rowData.Add(reader.GetInt32(reader.GetOrdinal("KnowledgeTypeId")).ToString());

            return rowData;
        }

        private static List<string> ExtractSyncRowData(SqlDataReader reader, Layer layer)
        {
            List<string> rowData = [GetFromReader(reader, "PairedItemSubject")];

            if (layer != Layer.Subject)
            {
                rowData.Add(GetFromReader(reader, "PairedItemPackage"));
                if (layer != Layer.Package)
                {
                    rowData.Add(GetFromReader(reader, "PairedItemTheme"));
                    if (layer != Layer.Theme)
                    {
                        rowData.Add(GetFromReader(reader, "PairedItemThemePart"));
                        rowData.Add(GetFromReader(reader, "PairedItemKnowledge"));
                        rowData.Add(GetFromReader(reader, "PairedItemKnowledgeType"));
                    }
                }
            }

            rowData.Add(reader.GetInt32(reader.GetOrdinal("PairedItemId")).ToString());

            return rowData;
        }

        private static string GetFromReader(SqlDataReader reader, string columnName)
        {
            int columnIndex = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(columnIndex))
                return "";

            return reader.GetString(columnIndex).Replace('\n', ' ').Trim();
        }

        public int Synchronize(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, Int32 subjectId) => _synchronizer.Synchronize(leftMetadata, rightMetadata, subjectId).Result;

        public int SaveAISyncChanges(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, int index = -1)
        {
            if (index != -1)
                return SavePair(Layer.SpecificKnowledge, leftMetadata.GetIdByRow(0), rightMetadata.GetIdByRow(index)) ? 1 : 0;
            else
                return _synchronizationCache.SaveAll(leftMetadata, rightMetadata);
        }

        public bool SavePair(Layer layer, Int32 leftId, Int32 rightId)
        {
            if (!_synchronizationCache.SetSynchronizedId(layer, leftId, rightId, true))
                return false;

            _synchronizationCache.SetSynchronizedMirroredId(layer, rightId, leftId);
            return true;
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
