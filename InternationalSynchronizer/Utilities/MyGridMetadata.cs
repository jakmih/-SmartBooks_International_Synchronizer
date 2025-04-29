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

        public MyGridMetadata(Layer layer, Int32 upperLayerId, bool isRightSide = false)
        {
            this._layer = layer;
            this._isRightSide = isRightSide;
            _dataTable = NewDataTable(layer, isRightSide);
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

        public void CopyMetadata(MyGridMetadata metadata)
        {
            _dataTable.Clear();
            _rowColors.Clear();
            _ids.Clear();
            _knowledgeTypeIds.Clear();
            for (int i = 0; i < metadata.RowCount(); i++)
                AddRow(metadata.GetRowData(i), metadata.GetRowColor(i), metadata.GetIdByRow(i), metadata.GetKnowledgeTypeIdByRow(i));
        }

        private static DataTable NewDataTable(Layer layer, bool reversed)
        {
            DataTable table = new();
            List<string> columns = ["Predmet"];
            if (layer != Layer.Subject)
            {
                columns.Add("Balíček");
                if (layer != Layer.Package)
                {
                    columns.Add("Téma");
                    if (layer != Layer.Theme)
                    {
                        columns.Add("Pod-Téma");
                        columns.Add("Úloha");
                        columns.Add("Typ úlohy");
                    }
                }
            }

            if (reversed)
                columns.Reverse();
            else if (layer != Layer.Knowledge && layer != Layer.KnowledgeType)
                columns.Add("Synchronizované");

            foreach (string column in columns)
                    table.Columns.Add(column, typeof(string));

            return table;
        }
    }
}
