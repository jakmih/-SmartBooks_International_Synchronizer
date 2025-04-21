using System.Data;
using System.Windows.Media;

namespace InternationalSynchronizer.Utilities
{
    public class MyGridMetadata
    {
        private readonly Layer layer;
        private readonly bool isRightSide;
        private readonly DataTable dataTable;
        private readonly List<SolidColorBrush> rowColors;
        private readonly List<Int32> ids;

        public MyGridMetadata(Layer layer, bool isRightSide = false)
        {
            this.layer = layer;
            this.isRightSide = isRightSide;
            dataTable = NewDataTable(layer, isRightSide);
            rowColors = [];
            ids = [];
        }

        public MyGridMetadata(MyGridMetadata metadata)
        {
            layer = metadata.layer;
            isRightSide = metadata.isRightSide;
            dataTable = metadata.dataTable.Copy();
            rowColors = [.. metadata.rowColors];
            ids = [.. metadata.ids];
        }

        public Layer GetLayer() => layer;

        public bool IsRightSide() => isRightSide;

        public DataTable GetDataTable() => dataTable;

        public string[] GetRowData(int row)
        {
            if (row < 0 || row >= dataTable.Rows.Count)
                return [];

            return [.. dataTable.Rows[row].ItemArray.Select(obj => obj?.ToString() ?? "")];
        }

        public void ClearRowData(int row)
        {
            if (row >= 0 && row < RowCount())
                for (int i = 0; i < ColumnCount(); i++)
                    dataTable.Rows[row][i] = "";
        }

        public List<SolidColorBrush> GetRowColors() => rowColors;

        public SolidColorBrush GetRowColor(int index)
        {
            if (index < 0 || index >= rowColors.Count)
                return new SolidColorBrush(Colors.Transparent);

            return rowColors[index];
        }

        public List<Int32> GetIds() => ids;

        public Int32 GetIdByRow(int row)
        {
            if (row < 0 || row >= ids.Count)
                return -1;

            return ids[row];
        }

        public void SetRowColor(int index, SolidColorBrush SYNCED_COLOR)
        {
            if (index < 0 || index >= rowColors.Count)
                return;

            rowColors[index] = SYNCED_COLOR;
        }

        public void AddRow(string[] row, SolidColorBrush color, Int32 id)
        {
            string[] paddedRow = new string[ColumnCount()];
            for (int i = 0; i < ColumnCount(); i++)
                paddedRow[i] = i < row.Length ? row[i] : "";

            dataTable.Rows.Add(paddedRow);
            rowColors.Add(color);
            ids.Add(id);
        }

        public bool IsRowColor(int index, SolidColorBrush color)
        {
            if (index < 0 || index >= RowCount())
                return false;

            return GetRowColor(index) == color;
        }

        public int RowCount() => dataTable.Rows.Count;

        public int ColumnCount() => dataTable.Columns.Count;

        public void CopyMetadata(MyGridMetadata metadata)
        {
            dataTable.Clear();
            rowColors.Clear();
            ids.Clear();
            for (int i = 0; i < metadata.RowCount(); i++)
                AddRow(metadata.GetRowData(i), metadata.GetRowColor(i), metadata.GetIdByRow(i));
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
                        if (layer != Layer.Knowledge)
                            columns.Add("Typ úlohy");
                    }
                }
            }

            if (reversed)
                columns.Reverse();

            foreach (string column in columns)
                table.Columns.Add(column, typeof(string));

            return table;
        }
    }
}
