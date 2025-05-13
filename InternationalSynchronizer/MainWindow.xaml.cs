using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using static InternationalSynchronizer.Utilities.AppColors;
using InternationalSynchronizer.Components;
using System.Windows.Input;

namespace InternationalSynchronizer
{
    public partial class MainWindow : Window
    {
        private Mode _mode = Mode.FilterData;
        private readonly DataManager _dataManager;
        private bool _isScrollSynchronizationEnabled = true;

        private MainWindow(Int32 mainDatabaseId, Int32 secondaryDatabaseId)
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            LeftDataGrid.ChangeTitle(App.Config["Titles:" + mainDatabaseId]!);
            LeftDataGrid.SelectionChanged += LeftDataGridSelectionChanged;
            LeftDataGrid.SetKnowledgePreviewBaseUrl(App.Config["Urls:" + mainDatabaseId]!);

            RightDataGrid.IsRightDataGrid(true);
            RightDataGrid.ChangeTitle(App.Config["Titles:" + secondaryDatabaseId]!);
            RightDataGrid.SelectionChanged += RightDataGridSelectionChanged;
            RightDataGrid.SetKnowledgePreviewBaseUrl(App.Config["Urls:" + secondaryDatabaseId]!);

            ActionButtons.AutoSync += AutoSynchronizationAsync;
            ActionButtons.ConfirmSync += ConfirmSynchronizationAsync;
            ActionButtons.ManualSync += ManualSynchronizationAsync;
            ActionButtons.DeleteSync += DeleteSynchronizationAsync;
            ActionButtons.NewDatabases += ChooseNewDatabases;

            FilterPanel.FiltereChanged += VisualiseFilteredDataAsync;

            _dataManager = new(mainDatabaseId, secondaryDatabaseId);
        }

        private async Task InitializeAsync()
        {
            await FilterPanel.LoadSubjectsAsync();
            Show();
            Activate();
            SetUpScrollSynchronization();
        }

        public static async Task<MainWindow> CreateAsync(Int32 mainDatabaseId, Int32 secondaryDatabaseId)
        {
            MainWindow window = new(mainDatabaseId, secondaryDatabaseId);
            await window.InitializeAsync();
            return window;
        }

        private void LeftDataGridSelectionChanged(int selectedIndex)
        {
            if (_mode == Mode.FilterData)
                FilterPanel.DataGridSelectionChanged(selectedIndex);
            else if (_mode == Mode.ManualSync && LeftDataGrid.GetMetadata().GetLayer() == Layer.Knowledge)
                LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
            else if (_mode == Mode.AutoSync)
            {
                if (FilterPanel.Filter.Layer == Layer.Knowledge)
                    LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
                if (FilterPanel.Filter.Layer != Layer.KnowledgeType)
                    AutoSyncSelectionChanged(selectedIndex);
            }
        }

        private void RightDataGridSelectionChanged(int selectedIndex)
        {
            if (_mode == Mode.ManualSync)
            {
                if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                    FilterPanel.DataGridSelectionChanged(selectedIndex);
                else if (RightDataGrid.GetMetadata().GetLayer() == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
            }
            else if (_mode == Mode.AutoSync)
            {
                if (FilterPanel.Filter.Layer == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
                if (FilterPanel.Filter.Layer == Layer.KnowledgeType)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
                else
                    AutoSyncSelectionChanged(selectedIndex);
            }
        }

        private void AutoSyncSelectionChanged(int selectedIndex)
        {
            LeftDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
            RightDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);

            if (FilterPanel.Filter.Layer != Layer.Knowledge)
                return;

            LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
            RightDataGrid.HandleKnowledgePreviews(selectedIndex);
        }

        private async Task VisualiseFilteredDataAsync()
        {
            FullData fullData;

            EnableComponents(false);
            ActionButtons.LoadingWindow.UpdateText("Načitava sa, prosím čakajte...");
            ActionButtons.LoadingWindow.Show();

            try
            {
                fullData = await Task.Run(() => _dataManager.GetFilterData(FilterPanel.Filter, _mode));
            }
            catch (SqlException)
            {
                ActionButtons.LoadingWindow.Hide();
                EnableComponents(true);
                throw;
            }

            ActionButtons.LoadingWindow.Hide();

            if (_mode == Mode.AutoSync)
                ExitAutoSyncMode();

            RightDataGrid.UpdateMetadata(fullData.RightMetadata);
            RightDataGrid.VisualizeGrid();

            if (_mode != Mode.ManualSync)
            {
                LeftDataGrid.UpdateMetadata(fullData.LeftMetadata);
                LeftDataGrid.VisualizeGrid();
            }
            else
                RightDataGrid.VisualiseSyncedItems(fullData.LeftMetadata);

            FilterPanel.UpdateFilter(fullData.FilterData);
            EnableComponents(true);
        }

        private async Task<int> AutoSynchronizationAsync()
        {
            if (FilterPanel.Filter.Layer == Layer.Subject)
                return -2;

            try
            {
                EnableComponents(false);

                int status = await Task.Run(() => _dataManager.Synchronize(LeftDataGrid.GetMetadata(),
                                                                          RightDataGrid.GetMetadata(),
                                                                          FilterPanel.Filter.GetSubjectId()));
                if (status > 0)
                {
                    if (FilterPanel.Filter.Layer != Layer.KnowledgeType)
                        LeftDataGrid.SetAutoSyncMetadata(RightDataGrid.GetMetadata());

                    RightDataGrid.SetAutoSyncMetadata(RightDataGrid.GetMetadata());
                    EnterAutoSyncMode();
                }

                return status;
            }
            finally
            {
                EnableComponents(true);
            }
        }

        private async Task<int> ConfirmSynchronizationAsync()
        {
            try
            {
                EnableComponents(false);

                return await (_mode == Mode.AutoSync ?  ConfirmAutoAsync() : ConfirmManualAsync());
            }
            finally
            {
                EnableComponents(true);
            }
        }

        private async Task<int> ConfirmAutoAsync()
        {
            MyGridMetadata rightMetadata = RightDataGrid.GetMetadata();

            int newSyncCount = 0;

            if (FilterPanel.Filter.Layer == Layer.KnowledgeType)
            {
                if (RightDataGrid.GetSelectedIndex() == -1)
                    return -4;

                int selectedIndex = RightDataGrid.GetSelectedIndex();

                newSyncCount = await Task.Run(() => _dataManager.SaveAISyncChanges(LeftDataGrid.GetMetadata(), rightMetadata, selectedIndex));

                MyGridMetadata newMetadata = new(Layer.KnowledgeType, -1, true, false);
                newMetadata.AddRow(rightMetadata.GetRowData(selectedIndex), NEUTRAL_COLOR, rightMetadata.GetIdByRow(selectedIndex));
                RightDataGrid.UpdateMetadata(newMetadata);
                RightDataGrid.VisualizeGrid();
            }
            else
            {
                newSyncCount = await Task.Run(() => _dataManager.SaveAISyncChanges(LeftDataGrid.GetMetadata(), rightMetadata));

                LeftDataGrid.ClearAISync();
                RightDataGrid.ClearAISync();
            }

            ExitAutoSyncMode();

            return newSyncCount;
        }

        private async Task<int> ConfirmManualAsync()
        {
            if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                return -1;

            int leftIndex = FilterPanel.Filter.Layer == Layer.KnowledgeType ? 0 : LeftDataGrid.GetSelectedIndex();
            int rightIndex = FilterPanel.Filter.Layer == Layer.KnowledgeType ? 0 : RightDataGrid.GetSelectedIndex();
            LeftDataGrid.SetSelectedIndex(-1);
            RightDataGrid.SetSelectedIndex(-1);

            if (leftIndex == -1 || rightIndex == -1)
                return -2;

            if (LeftDataGrid.IsRowColor(leftIndex, SYNCED_COLOR) || RightDataGrid.IsRowColor(rightIndex, SYNCED_COLOR))
                return -3;

            return await SynchronizePairAsync(leftIndex, rightIndex);
        }

        private async Task ManualSynchronizationAsync()
        {
            Mode modeSave = _mode;

            EnableComponents(false);
            ActionButtons.LoadingWindow.UpdateText("Načítava sa, prosím čakajte...");
            ActionButtons.LoadingWindow.Show();

            try
            {
                if (_mode == Mode.FilterData)
                    await EnterManualModeAsync();
                else
                    await ExitManualModeAsync();

                int leftColumn = Grid.GetColumn(FilterPanel);
                int rightColumn = Grid.GetColumn(ActionButtons);

                Grid.SetColumn(FilterPanel, rightColumn);
                Grid.SetColumn(ActionButtons, leftColumn);

                ActionButtons.ReverseButtons();
                EnableComponents(true);
            }
            catch (SqlException)
            {
                _mode = modeSave;
                throw;
            }
        }

        private async Task DeleteSynchronizationAsync()
        {
            if (_mode == Mode.AutoSync)
                DeleteAuto();
            else
                await DeleteManualAsync();
        }

        private void DeleteAuto()
        {
            MessageBoxResult cancel = MessageBox.Show("Naozaj chceš zrušiť AI synchronizáciu?",
                                                      "Potvrdenie zmazania", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (cancel == MessageBoxResult.Yes)
            {
                ExitAutoSyncMode();
                if (FilterPanel.Filter.Layer == Layer.KnowledgeType)
                {
                    MyGridMetadata newMetadata = new(Layer.KnowledgeType, -1, true, false);
                    newMetadata.AddRow([], NEUTRAL_COLOR, -1);
                    RightDataGrid.UpdateMetadata(newMetadata);
                    RightDataGrid.VisualizeGrid();
                }
                else
                {
                    LeftDataGrid.ClearAISync();
                    RightDataGrid.ClearAISync(true);
                }
            }
        }

        private async Task DeleteManualAsync()
        {
            int selectedIndex = FilterPanel.Filter.Layer == Layer.KnowledgeType ? 0 : RightDataGrid.GetSelectedIndex();

            if (selectedIndex == -1 || RightDataGrid.GetMetadata().GetIdByRow(selectedIndex) == -1)
            {
                MessageBox.Show("Na zrušenie synchronizácie položiek musíš označiť 1 synchronizovanú položku v pravej tabuľke.",
                                "Nevybrany pár", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult delete = MessageBox.Show("Naozaj chceš odstrániť túto synchronizáciu?",
                                                      "Potvrdenie zmazania", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (delete == MessageBoxResult.No)
                return;
            
            EnableComponents(false);
            ActionButtons.LoadingWindow.UpdateText("Odstraňuje sa synchronizácia, prosím čakajte...");
            ActionButtons.LoadingWindow.Show();

            try
            {
                await Task.Run(() => _dataManager.DeletePair(FilterPanel.Filter.Layer, LeftDataGrid.GetMetadata().GetIdByRow(selectedIndex)));

                RightDataGrid.ClearRowData(selectedIndex);
            }
            finally
            {
                EnableComponents(true);
            }
        }

        private void ChooseNewDatabases()
        {
            string message = "Naozaj chceš zmeniť databázy?";
            if (_mode == Mode.AutoSync)
                message += "\nVšetky neuložené synchronizácie budú stratené!";

            MessageBoxResult change = MessageBox.Show(message, "Potvrdenie zmeny databáz", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (change == MessageBoxResult.Yes)
            {
                ConnectionWindow connectionWindow = new();
                connectionWindow.Show();
                Close();
            }
        }

        private async Task<int> SynchronizePairAsync(int leftIndex, int rightIndex)
        {
            (int leftId, int rightId) = FilterPanel.GetIdsToSync(leftIndex, rightIndex);
            await Task.Run(() => _dataManager.SavePair(FilterPanel.Filter.Layer, leftId, rightId));

            LeftDataGrid.SetGridRowColor(leftIndex, SYNCED_COLOR);
            RightDataGrid.SetGridRowColor(rightIndex, SYNCED_COLOR);

            return 1;
        }

        private void EnterAutoSyncMode()
        {
            _mode = Mode.AutoSync;
            ActionButtons.EnterAutoSyncMode();
        }

        private void ExitAutoSyncMode()
        {
            _mode = Mode.FilterData;
            ActionButtons.EnterFilteringMode();
            LeftDataGrid.SetSelectedIndex(-1);
            RightDataGrid.SetSelectedIndex(-1);
            LeftDataGrid.HideKnowledgePreview();
            RightDataGrid.HideKnowledgePreview();
        }

        private async Task EnterManualModeAsync()
        {
            _mode = Mode.ManualSync;

            MyGridMetadata syncMetadata = new(RightDataGrid.GetMetadata());

            try
            {
                FilterPanel.SwapFilters();
                await VisualiseFilteredDataAsync();
            }
            catch (SqlException)
            {
                FilterPanel.SwapFilters();
                throw;
            }

            LeftDataGrid.VisualiseSyncedItems(syncMetadata);

            EnableScrollSynchronization(false);
            ActionButtons.EnterManualSyncMode();
        }

        private async Task ExitManualModeAsync()
        {
            _mode = Mode.FilterData;

            try
            {
                FilterPanel.SwapFilters();
                await VisualiseFilteredDataAsync();
            }
            catch (SqlException)
            {
                FilterPanel.SwapFilters();
                throw;
            }

            EnableScrollSynchronization(true);
            ActionButtons.EnterFilteringMode();
        }

        private void EnableComponents(bool enable)
        {
            if (enable)
            {
                Cursor = Cursors.Arrow;
                LeftDataGrid.IsEnabled = true;
                RightDataGrid.IsEnabled = true;
                ActionButtons.IsEnabled = true;
                FilterPanel.IsEnabled = true;
            }
            else
            {
                Cursor = Cursors.Wait;
                LeftDataGrid.IsEnabled = false;
                RightDataGrid.IsEnabled = false;
                ActionButtons.IsEnabled = false;
                FilterPanel.IsEnabled = false;
            }
        }

        private void EnableScrollSynchronization(bool enable) => _isScrollSynchronizationEnabled = enable;

        private void SetUpScrollSynchronization()
        {
            ScrollViewer leftSctollViewer = LeftDataGrid.GetScrollViewer();
            ScrollViewer rightSctollViewer = RightDataGrid.GetScrollViewer();
            if (leftSctollViewer == null || rightSctollViewer == null)
                return;

            leftSctollViewer.ScrollChanged += ScrollChanged;
            rightSctollViewer.ScrollChanged += ScrollChanged;
        }

        private void ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0 || !_isScrollSynchronizationEnabled)
                return;

            if (sender == LeftDataGrid.GetScrollViewer())
                RightDataGrid.GetScrollViewer().ScrollToVerticalOffset(LeftDataGrid.GetScrollViewer().VerticalOffset);

            else
                LeftDataGrid.GetScrollViewer().ScrollToVerticalOffset(RightDataGrid.GetScrollViewer().VerticalOffset);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e) => ActionButtons.LoadingWindow.Close();
    }
}