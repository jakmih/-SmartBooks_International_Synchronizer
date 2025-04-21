using InternationalSynchronizer.Utilities;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Components
{
    public partial class MyDataGrid : UserControl
    {
        private bool isRightDataGrid = false;
        private string knowledgePreviewBaseUrl = "";
        private readonly ScrollViewer scrollViewer;
        private MyGridMetadata metadata = new(Layer.Subject);
        public MyDataGrid()
        {
            InitializeComponent();
            if (DataGrid.Template.FindName("DG_ScrollViewer", DataGrid) is ScrollViewer tmp)
            {
                Debug.WriteLine("Found scrollViewer");
                scrollViewer = tmp;
            }
            else
            {
                Debug.WriteLine("ScrollViewer not found, creating a new one");
                scrollViewer = new();
            }
        }

        public void SetKnowledgePreviewBaseUrl(string url) => knowledgePreviewBaseUrl = url;

        public void IsRightDataGrid(bool isRight) => isRightDataGrid = isRight;

        public void ChangeTitle(string title) => Title.Text = title;

        public event Action<int>? SelectionChanged;

        private void DataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dataGrid || dataGrid.SelectedItem == null)
                return;

            SelectionChanged!.Invoke(dataGrid.SelectedIndex);
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int index = e.Row.GetIndex();
            if (sender.Equals(DataGrid) && index >= 0 && index < metadata.GetRowColors().Count)
                e.Row.Background = metadata.GetRowColor(index);
        }

        public void UpdateMetadata(MyGridMetadata newMetadata) => metadata = new(newMetadata);

        public void VisualizeDataGrid()
        {
            DataGrid.ItemsSource = metadata.GetDataTable().DefaultView;
            DataGrid.Items.Refresh();

            if (metadata.GetLayer() != Layer.KnowledgeType)
            {
                RowDefinition knowledgePreviewRow = (RowDefinition)FindName("KnowledgePreviewRow");
                knowledgePreviewRow.Height = new GridLength(0);
            }
            else
                HandleKnowledgePreviews(metadata.GetIdByRow(0));

            DataGrid.Columns.Clear();
            int columnCount = 0;
            foreach (DataColumn column in metadata.GetDataTable().Columns)
            {
                columnCount++;
                DataGridTextColumn textColumn = new()
                {
                    Header = column.ColumnName,
                    Binding = new Binding(column.ColumnName),
                    Width = (columnCount == metadata.ColumnCount())
                            ? new DataGridLength(1, DataGridLengthUnitType.Star)
                            : DataGridLength.Auto
                };
                DataGrid.Columns.Add(textColumn);
            }

            if (DataGrid.Items.Count > 0)
                DataGrid.ScrollIntoView(DataGrid.Items[0], DataGrid.Columns[isRightDataGrid ? 0 : ^1]);
        }

        public void HandleKnowledgePreviews(Int32 knowledgeId)
        {
            RowDefinition knowledgePreviewRow = (RowDefinition)FindName("KnowledgePreviewRow");
            knowledgePreviewRow.Height = new GridLength(1, GridUnitType.Star);

            string pass = $"&password={AppSettingsLoader.LoadConfiguration()["Keys:KnowledgePreview"]}";
            string knowledgePath = "/extern_knowledge_preview?knowledgeID=";
            
            KnowledgePreview.Source = new Uri(knowledgePreviewBaseUrl + knowledgePath + knowledgeId + pass);
        }

        public void SetGridRowColor(int index, SolidColorBrush color)
        {
            if (index < 0 || index >= metadata.GetRowColors().Count)
                return;

            metadata.SetRowColor(index, color);

            if (DataGrid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
                row.Background = color;
        }

        public void SetAutoSyncMetadata(MyGridMetadata newMetadata)
        {
            if (isRightDataGrid)
            {
                UpdateMetadata(newMetadata);
                VisualizeDataGrid();
                return;
            }

            for (int rowIndex = 0; rowIndex < metadata.RowCount() && rowIndex < newMetadata.RowCount(); rowIndex++)
                if (newMetadata.IsRowColor(rowIndex, ACCEPT_COLOR))
                    newMetadata.SetRowColor(rowIndex, ACCEPT_COLOR);

            DataGrid.UpdateLayout();
        }

        public void ClearAISync()
        {
            for (int rowIndex = 0; rowIndex < metadata.RowCount(); rowIndex++)
            {
                if (!metadata.IsRowColor(rowIndex, NEUTRAL_COLOR))
                {
                    metadata.SetRowColor(rowIndex, NEUTRAL_COLOR);
                    if (isRightDataGrid)
                        metadata.ClearRowData(rowIndex);
                }
            }
        }

        public void ChangeSynchronizationRowAcceptence(int row)
        {
            if (row < 0 || row >= metadata.RowCount())
                return;

            DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator.ContainerFromIndex(row);

            if (dataGridRow.Background == ACCEPT_COLOR || dataGridRow.Background == DECLINE_COLOR)
            {
                SolidColorBrush newColor = dataGridRow.Background == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                dataGridRow.Background = newColor;
                metadata.SetRowColor(row, newColor);
            }

            DataGrid.SelectedIndex = -1;
        }

        public int GetSelectedIndex() => DataGrid.SelectedIndex;

        public void SetSelectedIndex(int index) => DataGrid.SelectedIndex = index;

        public bool IsRowColor(int index, SolidColorBrush color) => metadata.IsRowColor(index, color);

        public MyGridMetadata GetMetadata() => metadata;

        public ScrollViewer GetScrollViewer() => scrollViewer;
        //public ScrollViewer? GetScrollViewer()
        //{

        //    if (grid is ScrollViewer viewer)
        //        return viewer;

        //    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
        //    {
        //        var child = VisualTreeHelper.GetChild(grid, i);
        //        var result = GetScrollViewer(child);
        //        if (result != null) return result;
        //    }

        //    return null;
        //}
    }
}
