using InternationalSynchronizer.Utilities;
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
        private ScrollViewer? scrollViewer = null;
        private MyGridMetadata metadata = new(Layer.Subject);

        public event Action<int>? SelectionChanged;

        public MyDataGrid()
        {
            InitializeComponent();
        }

        public void SetKnowledgePreviewBaseUrl(string url) => knowledgePreviewBaseUrl = url;

        public void IsRightDataGrid(bool isRight) => isRightDataGrid = isRight;

        public void ChangeTitle(string title) => Title.Text = title;

        private void ItemGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int index = e.Row.GetIndex();
            if (sender.Equals(ItemGrid) && index >= 0 && index < metadata.GetRowColors().Count)
                e.Row.Background = metadata.GetRowColor(index);
        }

        private void ItemGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            HighlightRow(sender, (DependencyObject)e.OriginalSource);
        }

        private void ItemGrid_MouseMove(object sender, MouseEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;

            while (element != null && element is not DataGridRow)
                element = VisualTreeHelper.GetParent(element);

            if (element is DataGridRow)
                ItemGrid.Cursor = Cursors.Hand;
            else
                ItemGrid.Cursor = Cursors.Arrow;

            if (e.LeftButton == MouseButtonState.Pressed)
                HighlightRow(sender, (DependencyObject)e.OriginalSource);
        }

        private void ItemGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HighlightRow(sender, (DependencyObject)e.OriginalSource);
            SelectionChanged?.Invoke(ItemGrid.SelectedIndex);
        }

        private void HighlightRow(object sender, DependencyObject dep)
        {
            if (!sender.Equals(ItemGrid))
                return;

            while (dep != null && dep is not DataGridRow)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }
        }

        public void UpdateMetadata(MyGridMetadata newMetadata) => metadata = new(newMetadata);

        public void VisualizeGrid(bool autoSync = false)
        {
            ItemGrid.Columns.Clear();
            ItemGrid.ItemsSource = metadata.GetDataTable().DefaultView;

            if (metadata.GetLayer() != Layer.KnowledgeType)
            {
                RowDefinition knowledgePreviewRow = (RowDefinition)FindName("KnowledgePreviewRow");
                knowledgePreviewRow.Height = new GridLength(0);
            }
            else
                HandleKnowledgePreviews(autoSync ? -1 : metadata.GetIdByRow(0));
            
            var columns = metadata.GetDataTable().Columns;
            for (int i = 0; i < columns.Count; i++)
            {
                DataGridTextColumn textColumn = new()
                {
                    Header = columns[i].ColumnName,
                    Binding = new Binding(columns[i].ColumnName),
                    Width = DataGridLength.Auto
                };
                ItemGrid.Columns.Add(textColumn);
            }
            
            if (ItemGrid.Items.Count > 0)
                ItemGrid.ScrollIntoView(ItemGrid.Items[0], ItemGrid.Columns[isRightDataGrid ? 0 : ^1]);
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

            if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
                row.Background = color;
        }

        public void SetAutoSyncMetadata(MyGridMetadata newMetadata)
        {
            if (isRightDataGrid)
            {
                UpdateMetadata(newMetadata);
                VisualizeGrid(true);
                return;
            }

            for (int rowIndex = 0; rowIndex < metadata.RowCount() && rowIndex < newMetadata.RowCount(); rowIndex++)
                if (newMetadata.IsRowColor(rowIndex, ACCEPT_COLOR))
                {
                    metadata.SetRowColor(rowIndex, ACCEPT_COLOR);
                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) is DataGridRow row)
                        row.Background = ACCEPT_COLOR;
                }
        }

        public void ClearAISync(bool delete = false)
        {
            for (int rowIndex = 0; rowIndex < metadata.RowCount(); rowIndex++)
            {
                if (metadata.IsRowColor(rowIndex, ACCEPT_COLOR) || metadata.IsRowColor(rowIndex, DECLINE_COLOR))
                {
                    if (delete || (isRightDataGrid && metadata.IsRowColor(rowIndex, DECLINE_COLOR)))
                        metadata.ClearRowData(rowIndex);

                    metadata.SetRowColor(rowIndex, NEUTRAL_COLOR);

                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) is DataGridRow row)
                        row.Background = NEUTRAL_COLOR;
                }
            }
        }

        public void ChangeSynchronizationRowAcceptence(int row)
        {
            if (row < 0 || row >= metadata.RowCount())
                return;

            DataGridRow dataGridRow = (DataGridRow)ItemGrid.ItemContainerGenerator.ContainerFromIndex(row);

            if (dataGridRow.Background == ACCEPT_COLOR || dataGridRow.Background == DECLINE_COLOR)
            {
                SolidColorBrush newColor = dataGridRow.Background == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                dataGridRow.Background = newColor;
                metadata.SetRowColor(row, newColor);
            }

            ItemGrid.SelectedIndex = -1;
        }

        public int GetSelectedIndex() => ItemGrid.SelectedIndex;

        public void SetSelectedIndex(int index) => ItemGrid.SelectedIndex = index;

        public bool IsRowColor(int index, SolidColorBrush color) => metadata.IsRowColor(index, color);

        public MyGridMetadata GetMetadata() => metadata;

        public ScrollViewer GetScrollViewer()
        {
            if (scrollViewer != null)
                return scrollViewer;

            if (ItemGrid.Template.FindName("DG_ScrollViewer", ItemGrid) is ScrollViewer tmp)
                scrollViewer = tmp;
            else
                return new();

            return scrollViewer;
        }
    }
}
