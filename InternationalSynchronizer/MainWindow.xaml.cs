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
        private DataManager _dataManager = null!;
        private bool _isScrollSynchronizationEnabled = true;

        private MainWindow(string mainDatabase, string secondaryDatabase)
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            LeftDataGrid.ChangeTitle(mainDatabase);
            LeftDataGrid.SelectionChanged += LeftDataGridSelectionChanged;
            LeftDataGrid.SetKnowledgePreviewBaseUrl(AppSettingsLoader.LoadConfiguration()["Urls:" + mainDatabase]!);

            RightDataGrid.IsRightDataGrid(true);
            RightDataGrid.ChangeTitle(secondaryDatabase);
            RightDataGrid.SelectionChanged += RightDataGridSelectionChanged;
            RightDataGrid.SetKnowledgePreviewBaseUrl(AppSettingsLoader.LoadConfiguration()["Urls:" + secondaryDatabase]!);

            ActionButtons.AutoSync += AutoSynchronizationAsync;
            ActionButtons.ConfirmSync += ConfirmSynchronizationAsync;
            ActionButtons.ManualSync += ManualSynchronizationAsync;
            ActionButtons.DeleteSync += DeleteSynchronizationAsync;
            ActionButtons.NewDatabases += ChooseNewDatabases;

            FilterPanel.FiltereChanged += VisualiseFilteredDataAsync;
        }

        private async Task InitializeAsync(string mainDatabase, string secondaryDatabase)
        {
            _dataManager = await DataManager.CreateAsync(mainDatabase, secondaryDatabase);
            await FilterPanel.LoadSubjectsAsync();
            Show();
            Activate();
            SetUpScrollSynchronization();
        }
        public static async Task<MainWindow> CreateAsync(string mainDb, string secondaryDb)
        {
            var window = new MainWindow(mainDb, secondaryDb);
            await window.InitializeAsync(mainDb, secondaryDb);
            return window;
        }

        private void LeftDataGridSelectionChanged(int selectedIndex)
        {
            if (_mode == Mode.FilterData)
                FilterPanel.DataGridSelectionChanged(selectedIndex);
            else if (_mode == Mode.ManualSync && LeftDataGrid.GetMetadata().GetLayer() == Layer.Knowledge)
                LeftDataGrid.HandleKnowledgePreviews(LeftDataGrid.GetMetadata().GetIdByRow(selectedIndex));
            else if (_mode == Mode.AutoSync && LeftDataGrid.GetMetadata().GetLayer() != Layer.KnowledgeType)
            {
                LeftDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                RightDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
            }
        }

        private void RightDataGridSelectionChanged(int selectedIndex)
        {
            if (_mode == Mode.ManualSync)
            {
                if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                    FilterPanel.DataGridSelectionChanged(selectedIndex);
                else if (RightDataGrid.GetMetadata().GetLayer() == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(RightDataGrid.GetMetadata().GetIdByRow(selectedIndex));
            }
            else if (_mode == Mode.AutoSync)
            {
                if (FilterPanel.Filter.Layer == Layer.KnowledgeType)
                    RightDataGrid.HandleKnowledgePreviews(RightDataGrid.GetMetadata().GetIdByRow(selectedIndex));
                else
                {
                    LeftDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                    RightDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                }
            }
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
            catch (Exception ex)
            {
                ActionButtons.LoadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                EnableComponents(true);
                throw;
            }

            ActionButtons.LoadingWindow.Hide();

            if (_mode == Mode.AutoSync)
                ExitAutoSyncMode();

            if (_mode != Mode.ManualSync)
            {
                LeftDataGrid.UpdateMetadata(fullData.LeftMetadata);
                LeftDataGrid.VisualizeGrid();
            }

            RightDataGrid.UpdateMetadata(fullData.RightMetadata);
            RightDataGrid.VisualizeGrid();

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

                MyGridMetadata newMetadata = new(Layer.KnowledgeType, -1, true);
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

            _dataManager.AddToSyncedChildCount(FilterPanel.Filter.Layer - 1, FilterPanel.Filter.GetUpperLayerId(), newSyncCount);

            ExitAutoSyncMode();

            return newSyncCount;
        }

        private async Task<int> ConfirmManualAsync()
        {
            int leftIndex = LeftDataGrid.GetMetadata().GetLayer() == Layer.KnowledgeType ? 0 : LeftDataGrid.GetSelectedIndex();
            int rightIndex = LeftDataGrid.GetMetadata().GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.GetSelectedIndex();
            LeftDataGrid.SetSelectedIndex(-1);
            RightDataGrid.SetSelectedIndex(-1);

            if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                return -1;
            else if (leftIndex == -1 || rightIndex == -1)
                return -2;
            else if (LeftDataGrid.IsRowColor(leftIndex, SYNCED_COLOR) || RightDataGrid.IsRowColor(rightIndex, SYNCED_COLOR))
                return -3;
            else
            {
                int newSyncCount = await SynchronizePairAsync(leftIndex, rightIndex);

                _dataManager.AddToSyncedChildCount(FilterPanel.Filter.Layer - 1, FilterPanel.FilterStorage.GetUpperLayerId(), newSyncCount);
                
                return newSyncCount;
            }
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
                    MyGridMetadata newMetadata = new(Layer.KnowledgeType, -1, true);
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
            if (delete == MessageBoxResult.Yes)
            {
                EnableComponents(false);
                ActionButtons.LoadingWindow.UpdateText("Odstraňuje sa synchronizácia, prosím čakajte...");
                ActionButtons.LoadingWindow.Show();

                try
                {
                    int newSyncCount = await Task.Run(() => _dataManager.DeletePair(FilterPanel.Filter.Layer, LeftDataGrid.GetMetadata().GetIdByRow(selectedIndex)));

                    _dataManager.AddToSyncedChildCount(FilterPanel.Filter.Layer - 1, FilterPanel.Filter.GetUpperLayerId(), newSyncCount);

                    RightDataGrid.GetMetadata().ClearRowData(selectedIndex);
                }
                finally
                {
                    EnableComponents(true);
                }
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
        }

        private async Task EnterManualModeAsync()
        {
            _mode = Mode.ManualSync;

            MyGridMetadata newLeftMetadata = new(LeftDataGrid.GetMetadata());
            await Task.Run(() => _dataManager.VisualiseSyncedItems(newLeftMetadata, FilterPanel.Filter));
            
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

            LeftDataGrid.UpdateMetadata(newLeftMetadata);
            LeftDataGrid.VisualizeGrid();

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

        private void MainWindow_Closing(object? sender, CancelEventArgs e) => ActionButtons.LoadingWindow.Close();

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
    }
}