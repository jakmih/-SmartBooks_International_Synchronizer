using InternationalSynchronizer.Utilities;
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
        private bool _isRightDataGrid = false;
        private string _knowledgePreviewBaseUrl = "";
        private ScrollViewer? _scrollViewer = null;
        private MyGridMetadata _metadata = new(Layer.Subject, -1);
        public MyGridMetadata Metadata => _metadata;

        public event Action<int>? SelectionChanged;

        public MyDataGrid()
        {
            InitializeComponent();
        }

        public void SetKnowledgePreviewBaseUrl(string url) => _knowledgePreviewBaseUrl = url;

        public void IsRightDataGrid(bool isRight) => _isRightDataGrid = isRight;

        private void ItemGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            int index = e.Row.GetIndex();
            if (sender.Equals(ItemGrid) && index >= 0 && index < _metadata.GetRowColors().Count)
                e.Row.Background = _metadata.GetRowColor(index);
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

        public void UpdateMetadata(MyGridMetadata newMetadata, bool autoSync = false)
        {
            _metadata = new(newMetadata);
            ItemGrid.Columns.Clear();
            ItemGrid.ItemsSource = _metadata.GetDataTable().DefaultView;

            if (_metadata.GetLayer() != Layer.SpecificKnowledge)
                HideKnowledgePreview();
            else
                HandleKnowledgePreviews(autoSync ? -1 : 0);

            var columns = _metadata.GetDataTable().Columns;
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
                ItemGrid.ScrollIntoView(ItemGrid.Items[0], ItemGrid.Columns[_isRightDataGrid ? 0 : ^1]);
        }

        public void VisualiseSyncedItems(MyGridMetadata syncMetadata)
        {
            for (int i = 0; i < _metadata.RowCount() && i < syncMetadata.RowCount(); i++)
                if (syncMetadata.GetIdByRow(i) != -1)
                {
                    _metadata.SetRowColor(i, SYNCED_COLOR);
                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                        row.Background = SYNCED_COLOR;
                }
                else
                {
                    _metadata.SetRowColor(i, NEUTRAL_COLOR);
                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                        row.Background = NEUTRAL_COLOR;
                }
        }

        public void HideKnowledgePreview()
        {
            RowDefinition knowledgePreviewRow = (RowDefinition)FindName("KnowledgePreviewRow");
            knowledgePreviewRow.Height = new GridLength(0);
        }

        public void HandleKnowledgePreviews(Int32 rowIndex)
        {
            RowDefinition knowledgePreviewRow = (RowDefinition)FindName("KnowledgePreviewRow");
            knowledgePreviewRow.Height = new GridLength(1, GridUnitType.Star);

            string pass = $"&password={App.Config["Keys:KnowledgePreview"]}";
            string knowledgePath = "/extern_knowledge_preview?knowledgeID=";

            Int32 knowledgeId = _metadata.GetIdByRow(rowIndex);

            KnowledgePreview.Source = new Uri(_knowledgePreviewBaseUrl + knowledgePath + knowledgeId + pass);
        }

        public void SetGridRowColor(int index, SolidColorBrush color)
        {
            if (index < 0 || index >= _metadata.GetRowColors().Count)
                return;

            _metadata.SetRowColor(index, color);

            if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
                row.Background = color;
        }

        public void SetAutoSyncMetadata(MyGridMetadata newMetadata)
        {
            if (_isRightDataGrid)
            {
                UpdateMetadata(newMetadata, true);
                return;
            }

            for (int rowIndex = 0; rowIndex < _metadata.RowCount() && rowIndex < newMetadata.RowCount(); rowIndex++)
                if (newMetadata.IsRowColor(rowIndex, ACCEPT_COLOR))
                {
                    _metadata.SetRowColor(rowIndex, ACCEPT_COLOR);
                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) is DataGridRow row)
                        row.Background = ACCEPT_COLOR;
                }
        }

        public void ClearAISync(bool delete = false, bool hasChildren = false)
        {
            for (int rowIndex = 0; rowIndex < _metadata.RowCount(); rowIndex++)
            {
                if (_metadata.IsRowColor(rowIndex, ACCEPT_COLOR) || _metadata.IsRowColor(rowIndex, DECLINE_COLOR))
                {
                    if (delete || (_isRightDataGrid && _metadata.IsRowColor(rowIndex, DECLINE_COLOR)))
                        _metadata.ClearRowData(rowIndex);

                    SolidColorBrush color = hasChildren ? _metadata.GetColor(rowIndex) : NEUTRAL_COLOR;

                    _metadata.SetRowColor(rowIndex, color);

                    if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) is DataGridRow row)
                        row.Background = color;
                }
            }

            HideKnowledgePreview();
        }

        public void ClearRowData(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= _metadata.RowCount())
                return;

            _metadata.ClearRowData(selectedIndex);
            if (ItemGrid.ItemContainerGenerator.ContainerFromIndex(selectedIndex) is DataGridRow row)
                row.Background = NEUTRAL_COLOR;
        }

        public void ChangeSynchronizationRowAcceptence(int row)
        {
            if (row < 0 || row >= _metadata.RowCount())
                return;

            DataGridRow dataGridRow = (DataGridRow)ItemGrid.ItemContainerGenerator.ContainerFromIndex(row);

            if (dataGridRow.Background == ACCEPT_COLOR || dataGridRow.Background == DECLINE_COLOR)
            {
                SolidColorBrush newColor = dataGridRow.Background == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                dataGridRow.Background = newColor;
                _metadata.SetRowColor(row, newColor);
            }

            ItemGrid.SelectedIndex = -1;
        }

        public bool IsRowColor(int index, SolidColorBrush color) => _metadata.IsRowColor(index, color);

        public ScrollViewer GetScrollViewer()
        {
            if (_scrollViewer != null)
                return _scrollViewer;

            if (ItemGrid.Template.FindName("DG_ScrollViewer", ItemGrid) is ScrollViewer tmp)
                _scrollViewer = tmp;
            else
                return new();

            return _scrollViewer;
        }
    }
}
