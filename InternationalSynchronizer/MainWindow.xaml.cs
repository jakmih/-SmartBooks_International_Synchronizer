using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using static InternationalSynchronizer.Utilities.AppColors;
using InternationalSynchronizer.Components;
using System.Windows.Input;
using System.Diagnostics;

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

            LeftDataGrid.Title.Text = App.Config["Titles:" + mainDatabaseId]!;
            LeftDataGrid.SelectionChanged += LeftDataGridSelectionChanged;
            LeftDataGrid.SetKnowledgePreviewBaseUrl(App.Config["Urls:" + mainDatabaseId]!);

            RightDataGrid.IsRightDataGrid(true);
            RightDataGrid.Title.Text = App.Config["Titles:" + secondaryDatabaseId]!;
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
            else if (_mode == Mode.ManualSync && LeftDataGrid.Metadata.GetLayer() == Layer.Knowledge)
                LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
            else if (_mode == Mode.AutoSync)
            {
                if (FilterPanel.Filter.Layer == Layer.Knowledge)
                    LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
                if (FilterPanel.Filter.Layer != Layer.SpecificKnowledge)
                    AutoSyncSelectionChanged(selectedIndex);
            }
        }

        private void RightDataGridSelectionChanged(int selectedIndex)
        {
            if (_mode == Mode.ManualSync)
            {
                if (LeftDataGrid.Metadata.GetLayer() != RightDataGrid.Metadata.GetLayer())
                    FilterPanel.DataGridSelectionChanged(selectedIndex);
                else if (RightDataGrid.Metadata.GetLayer() == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
            }
            else if (_mode == Mode.AutoSync)
            {
                if (FilterPanel.Filter.Layer == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
                if (FilterPanel.Filter.Layer == Layer.SpecificKnowledge)
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

            if (_mode != Mode.ManualSync)
            {
                LeftDataGrid.UpdateMetadata(fullData.MainMetadata);
                RightDataGrid.UpdateMetadata(fullData.SyncMetadata);
            }
            else
            {
                RightDataGrid.UpdateMetadata(fullData.MainMetadata);
                RightDataGrid.VisualiseSyncedItems(fullData.SyncMetadata);
            }

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

                int status = await Task.Run(() => _dataManager.Synchronize(LeftDataGrid.Metadata,
                                                                          RightDataGrid.Metadata,
                                                                          FilterPanel.Filter.GetSubjectId()));
                if (status > 0)
                {
                    if (FilterPanel.Filter.Layer != Layer.SpecificKnowledge)
                        LeftDataGrid.SetAutoSyncMetadata(RightDataGrid.Metadata);

                    RightDataGrid.SetAutoSyncMetadata(RightDataGrid.Metadata);
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
            MyGridMetadata rightMetadata = RightDataGrid.Metadata;

            int newSyncCount = 0;

            if (FilterPanel.Filter.Layer == Layer.SpecificKnowledge)
            {
                if (RightDataGrid.ItemGrid.SelectedIndex == -1)
                    return -4;

                int selectedIndex = RightDataGrid.ItemGrid.SelectedIndex;

                newSyncCount = await Task.Run(() => _dataManager.SaveAISyncChanges(LeftDataGrid.Metadata, rightMetadata, selectedIndex));

                MyGridMetadata newMetadata = new(Layer.SpecificKnowledge, -1, true, false);

                if (newSyncCount == 0)
                {
                    newSyncCount = -5;
                    newMetadata.AddRow([], NEUTRAL_COLOR, -1);
                }
                else
                    newMetadata.AddRow(rightMetadata.GetRowData(selectedIndex), NEUTRAL_COLOR, rightMetadata.GetIdByRow(selectedIndex));

                RightDataGrid.UpdateMetadata(newMetadata);
            }
            else
            {
                newSyncCount = await Task.Run(() => _dataManager.SaveAISyncChanges(LeftDataGrid.Metadata, rightMetadata));

                LeftDataGrid.ClearAISync(hasChildren: true);
                RightDataGrid.ClearAISync();
            }

            ExitAutoSyncMode();

            return newSyncCount;
        }

        private async Task<int> ConfirmManualAsync()
        {
            if (LeftDataGrid.Metadata.GetLayer() != RightDataGrid.Metadata.GetLayer())
                return -1;

            int leftIndex = FilterPanel.Filter.Layer == Layer.SpecificKnowledge ? 0 : LeftDataGrid.ItemGrid.SelectedIndex;
            int rightIndex = FilterPanel.Filter.Layer == Layer.SpecificKnowledge ? 0 : RightDataGrid.ItemGrid.SelectedIndex;
            LeftDataGrid.ItemGrid.SelectedIndex = -1;
            RightDataGrid.ItemGrid.SelectedIndex = -1;

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
                if (FilterPanel.Filter.Layer == Layer.SpecificKnowledge)
                {
                    MyGridMetadata newMetadata = new(Layer.SpecificKnowledge, -1, true, false);
                    newMetadata.AddRow([], NEUTRAL_COLOR, -1);
                    RightDataGrid.UpdateMetadata(newMetadata);
                }
                else
                {
                    LeftDataGrid.ClearAISync(hasChildren: true);
                    RightDataGrid.ClearAISync(delete: true);
                }
            }
        }

        private async Task DeleteManualAsync()
        {
            int selectedIndex = FilterPanel.Filter.Layer == Layer.SpecificKnowledge ? 0 : RightDataGrid.ItemGrid.SelectedIndex;

            if (selectedIndex == -1 || RightDataGrid.Metadata.GetIdByRow(selectedIndex) == -1)
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
                await Task.Run(() => _dataManager.DeletePair(FilterPanel.Filter.Layer, LeftDataGrid.Metadata.GetIdByRow(selectedIndex)));

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
            Debug.WriteLine($"Synchronizing: {leftId} {rightId}");
            bool saved = await Task.Run(() => _dataManager.SavePair(FilterPanel.Filter.Layer, leftId, rightId));

            if (!saved)
                return 0;

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
            LeftDataGrid.ItemGrid.SelectedIndex = -1;
            RightDataGrid.ItemGrid.SelectedIndex = -1;
            LeftDataGrid.HideKnowledgePreview();
            RightDataGrid.HideKnowledgePreview();
        }

        private async Task EnterManualModeAsync()
        {
            _mode = Mode.ManualSync;

            MyGridMetadata syncMetadata = new(RightDataGrid.Metadata);

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