using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using static InternationalSynchronizer.Utilities.SqlQuery;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Utilities
{
    public class DataManager
    {
        private readonly string mainConnectionString;
        private readonly string secondaryConnectionString;
        private readonly ItemCache itemCache;
        private readonly SynchronizationCache synchronizationCache;
        private readonly Synchronizer synchronizer;

        public DataManager(string mainDatabase, string secondaryDatabase)
        {
            IConfiguration config = AppSettingsLoader.LoadConfiguration();
            mainConnectionString = config.GetConnectionString(mainDatabase)!;
            secondaryConnectionString = config.GetConnectionString(secondaryDatabase)!;
            itemCache = new(mainConnectionString, secondaryConnectionString);
            synchronizationCache = new(config.GetConnectionString("Sync")!, mainDatabase, secondaryDatabase);
            synchronizer = new(this);
        }

        public FullData GetFilterData(Filter filter, Mode mode)
        {
            List<string> filterData = [];
            MyGridMetadata leftMetadata = new(filter.GetLayer());
            MyGridMetadata rightMetadata = new(filter.GetLayer(), true);

            if (mode != Mode.ManualSync)
            {
                SetDataTable(leftMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(leftMetadata.GetIds());
                SetSynchonizedDataTable(filter, rightMetadata);
            }
            else
            {
                SetDataTable(rightMetadata, filterData, filter.GetUpperLayerId());
                filter.SetIds(rightMetadata.GetIds());
                VisualiseSyncedItems(rightMetadata, filter);
            }

            return new FullData(leftMetadata, rightMetadata, filterData, filter.GetLayer());
        }

        private void SetDataTable(MyGridMetadata metadata, List<string> filterData, Int32 selectedItemId)
        {
            string connectionString = metadata.IsRightSide() ? secondaryConnectionString : mainConnectionString;
            string query = GetItemQuery(metadata.GetLayer(), selectedItemId, true);

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                string[] row = ExtractRowData(reader, metadata.IsRightSide());

                filterData.Add(metadata.IsRightSide() ? row[0] : row[^1]);

                metadata.AddRow(row, NEUTRAL_COLOR, reader.GetInt32(reader.FieldCount - 1));
            }
        }

        private void SetSynchonizedDataTable(Filter filter, MyGridMetadata metadata)
        {
            foreach (Int32 id in synchronizationCache.GetSynchronizedIds(filter, false))
            {
                string[] row;
                if (id == -1 || !TryGetRightRow(id, out List<string> rightRow, filter.GetLayer()))
                    row = [];
                else
                    row = [.. rightRow];

                metadata.AddRow(row, NEUTRAL_COLOR, id);
            }
        }

        private static string[] ExtractRowData(SqlDataReader reader, bool mirrorRowData)
        {
            List<string> rowData = [];
            for (int i = 0; i < reader.FieldCount - 1; i++)
                rowData.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            if (mirrorRowData)
                rowData.Reverse();

            return [.. rowData];
        }

        private bool TryGetRightRow(int id, out List<string> row, Layer layer)
        {
            row = [.. itemCache.GetItem(layer, id)];

            if (row.IsNullOrEmpty())
                return false;

            row.Reverse();
            return true;
        }

        public void VisualiseSyncedItems(MyGridMetadata metadata, Filter filter)
        {
            int index = 0;
            foreach (Int32 id in synchronizationCache.GetSynchronizedIds(filter, metadata.IsRightSide()))
            {
                if (id != -1)
                    metadata.SetRowColor(index, SYNCED_COLOR);
                index++;
            }
        }

        public int Synchronize(Filter filter, MyGridMetadata newMetadata, MyGridMetadata leftMetadata) => synchronizer.Synchronize(filter, newMetadata, leftMetadata, itemCache);

        public void SaveAISyncChanges(MyGridMetadata leftMetadata, MyGridMetadata rightMetadata, int index = -1)
        {
            if (index != -1)
            {
                SavePair(Layer.KnowledgeType, leftMetadata.GetIdByRow(0), rightMetadata.GetIdByRow(index));
                return;
            }

            for (int i = 0; i < rightMetadata.RowCount(); i++)
                if (rightMetadata.IsRowColor(i, ACCEPT_COLOR))
                    SavePair(rightMetadata.GetLayer(), leftMetadata.GetIdByRow(i), rightMetadata.GetIdByRow(i));
        }

        public void SavePair(Layer layer, Int32 leftId, Int32 rightId)
        {
            synchronizationCache.SetSynchronizedId(layer, leftId, rightId, true);
            synchronizationCache.SetSynchronizedMirroredId(layer, rightId, leftId);
        }

        public void DeletePair(Layer layer, Int32 id) => synchronizationCache.DeletePair(layer, id);

        public Int32 GetSynchronizedId(Layer layer, Int32 itemId, bool secondaryDatabaseSearch = false) => synchronizationCache.GetSynchronizedId(layer, itemId, secondaryDatabaseSearch);

        public Int32 GetSecondaryDatabaseId() => synchronizationCache.GetSecondaryDatabaseId();
    }
}
