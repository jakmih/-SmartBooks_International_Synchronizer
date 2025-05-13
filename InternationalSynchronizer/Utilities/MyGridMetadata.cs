using Microsoft.Data.SqlClient;
using System.Data;
using System.Windows.Media;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Utilities
{
    public class MyGridMetadata
    {
        private readonly Layer _layer;
        private readonly bool _isRightSide;
        private readonly DataTable _dataTable;
        private readonly List<SolidColorBrush> _rowColors;
        private readonly List<Int32> _ids;
        private readonly List<Int32> _knowledgeTypeIds;
        private readonly Int32 _upperLayerId;
        public Int32 UpperLayerId => _upperLayerId;

        public MyGridMetadata(Layer layer, Int32 upperLayerId, bool isRightSide = false, bool includeChildCount = true)
        {
            _layer = layer;
            _isRightSide = isRightSide;
            _dataTable = NewDataTable(layer, isRightSide, includeChildCount);
            _rowColors = [];
            _ids = [];
            _knowledgeTypeIds = [];
            _upperLayerId = upperLayerId;
        }

        public MyGridMetadata(MyGridMetadata metadata)
        {
            _layer = metadata._layer;
            _isRightSide = metadata._isRightSide;
            _dataTable = metadata._dataTable.Copy();
            _rowColors = [.. metadata._rowColors];
            _ids = [.. metadata._ids];
            _knowledgeTypeIds = [.. metadata._knowledgeTypeIds];
            _upperLayerId = metadata._upperLayerId;
        }

        public Layer GetLayer() => _layer;

        public bool IsRightSide() => _isRightSide;

        public DataTable GetDataTable() => _dataTable;

        public string[] GetRowData(int row)
        {
            if (row < 0 || row >= _dataTable.Rows.Count)
                return [.. Enumerable.Repeat("", RowCount())];

            return [.. _dataTable.Rows[row].ItemArray.Select(obj => obj?.ToString() ?? "")];
        }

        public void ClearRowData(int row)
        {
            if (row >= 0 && row < RowCount())
                for (int i = 0; i < ColumnCount(); i++)
                    _dataTable.Rows[row][i] = "";
            _ids[row] = -1;
            _rowColors[row] = NEUTRAL_COLOR;
        }

        public List<SolidColorBrush> GetRowColors() => _rowColors;

        public SolidColorBrush GetRowColor(int index)
        {
            if (index < 0 || index >= _rowColors.Count)
                return new SolidColorBrush(Colors.Transparent);

            return _rowColors[index];
        }

        public List<Int32> GetIds() => _ids;

        public Int32 GetIdByRow(int row)
        {
            if (row < 0 || row >= _ids.Count)
                return -1;

            return _ids[row];
        }

        public Int32 GetKnowledgeTypeIdByRow(int row)
        {
            if (row < 0 || row >= _knowledgeTypeIds.Count)
                return -1;

            return _knowledgeTypeIds[row];
        }

        public void SetRowColor(int index, SolidColorBrush color)
        {
            if (index < 0 || index >= _rowColors.Count)
                return;

            _rowColors[index] = color;
        }

        public void AddRow(string[] row, SolidColorBrush color, Int32 id, Int32 knowledgeTypeId = -1)
        {
            string[] paddedRow = new string[ColumnCount()];
            for (int i = 0; i < ColumnCount(); i++)
                paddedRow[i] = i < row.Length ? row[i] : "";

            _dataTable.Rows.Add(paddedRow);
            _rowColors.Add(color);
            _ids.Add(id);
            _knowledgeTypeIds.Add(knowledgeTypeId);
        }

        public bool IsRowColor(int index, SolidColorBrush color)
        {
            if (index < 0 || index >= RowCount())
                return false;

            return GetRowColor(index) == color;
        }

        public int RowCount() => _dataTable.Rows.Count;

        public int ColumnCount() => _dataTable.Columns.Count;

        public void RemoveData()
        {
            _dataTable.Clear();
            _rowColors.Clear();
            _ids.Clear();
            _knowledgeTypeIds.Clear();
        }

        public void UpdateRowId(int rowIndex, Int32 id)
        {
            if (rowIndex < 0 || rowIndex >= RowCount())
                return;

            _ids[rowIndex] = id;
        }

        public void UpdateRow(int rowIndex, string[] row, SolidColorBrush color)
        {
            if (rowIndex < 0 || rowIndex >= RowCount())
                return;

            for (int i = 0; i < ColumnCount(); i++)
                _dataTable.Rows[rowIndex][i] = i < row.Length ? row[i] : "";

            _rowColors[rowIndex] = color;
        }

        public void ExtractRowData(SqlDataReader reader, bool isMain)
        {
            List<string> rowData = isMain ? ExtractMainRowData(reader) : ExtractSyncRowData(reader);

            if (_isRightSide)
                rowData.Reverse();

            if (isMain)
                AddRow([.. rowData],
                       GetColor(rowData),
                       reader.GetInt32(reader.GetOrdinal("Id")),
                       reader.GetInt32(reader.GetOrdinal("KnowledgeTypeId")));
            else
                AddRow([.. rowData],
                       rowData[_layer == Layer.Knowledge ? 1 : 0].StartsWith("POLOŽKA BOLA ODSTRÁNENÁ - ID:") ? DELETED_ITEM_COLOR : NEUTRAL_COLOR,
                       reader.GetInt32(reader.GetOrdinal("PairedItemId")));
        }

        private List<string> ExtractMainRowData(SqlDataReader reader)
        {
            List<string> rowData = [GetFromReader(reader, "Subject")];

            if (_layer != Layer.Subject)
            {
                rowData.Add(GetFromReader(reader, "Package"));
                if (_layer != Layer.Package)
                {
                    rowData.Add(GetFromReader(reader, "Theme"));
                    if (_layer != Layer.Theme)
                    {
                        rowData.Add(GetFromReader(reader, "ThemePart"));
                        rowData.Add(GetFromReader(reader, "Knowledge"));
                        rowData.Add(GetFromReader(reader, "KnowledgeType"));
                    }
                }
            }
            if (_layer != Layer.Knowledge && _layer != Layer.KnowledgeType)
                rowData.Add(GetFromReader(reader, "SyncedChildrenRatio"));

            return rowData;
        }

        private List<string> ExtractSyncRowData(SqlDataReader reader)
        {
            List<string> rowData = [GetFromReader(reader, "PairedItemSubject")];

            if (_layer != Layer.Subject)
            {
                rowData.Add(GetFromReader(reader, "PairedItemPackage"));
                if (_layer != Layer.Package)
                {
                    rowData.Add(GetFromReader(reader, "PairedItemTheme"));
                    if (_layer != Layer.Theme)
                    {
                        rowData.Add(GetFromReader(reader, "PairedItemThemePart"));
                        rowData.Add(GetFromReader(reader, "PairedItemKnowledge"));
                        rowData.Add(GetFromReader(reader, "PairedItemKnowledgeType"));
                    }
                }
            }

            return rowData;
        }

        private SolidColorBrush GetColor(List<string> rowData)
        {
            if (_layer == Layer.Knowledge || _layer == Layer.KnowledgeType)
                return NEUTRAL_COLOR;

            string[] childCount = rowData[_isRightSide ? 0 : ^1].Split('/');

            int syncedChildren = Int32.Parse(childCount[0]);
            int totalChildren = Int32.Parse(childCount[1]);

            if (syncedChildren == 0)
                return NO_CHILDREN_SYNCED_COLOR;

            if (syncedChildren == totalChildren)
                return ALL_CHILDREN_SYNCED_COLOR;

            return PARTIAL_CHILDREN_SYNCED_COLOR;
        }

        private static string GetFromReader(SqlDataReader reader, string columnName)
        {
            int columnIndex = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(columnIndex))
                return "";

            return reader.GetString(columnIndex).Replace('\n', ' ').Trim();
        }

        private static DataTable NewDataTable(Layer layer, bool reversed, bool includeChildCount)
        {
            DataTable table = new();
            List<string> columns = ["Subject"];
            if (layer != Layer.Subject)
            {
                columns.Add("Package");
                if (layer != Layer.Package)
                {
                    columns.Add("Theme");
                    if (layer != Layer.Theme)
                    {
                        columns.Add("Theme-part");
                        columns.Add("Task");
                        columns.Add("Typ úlohy");
                    }
                }
            }

            if (layer != Layer.Knowledge && layer != Layer.KnowledgeType && includeChildCount)
                columns.Add("Children Synchronized");

            if (reversed)
                columns.Reverse();
            
            foreach (string column in columns)
                    table.Columns.Add(column, typeof(string));

            return table;
        }
    }
}
