using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using static InternationalSynchronizer.Utilities.AppColors;
using InternationalSynchronizer.Components;

namespace InternationalSynchronizer
{
    public partial class MainWindow : Window
    {
        private Mode mode = Mode.FilterData;
        private readonly DataManager dataManager;
        private bool isScrollSynchronizationEnabled = true;

        public MainWindow() : this("Slovenská", "Česká") { } //Debug purposes

        public MainWindow(string mainDatabase, string secondaryDatabase)
        {
            InitializeComponent();
            Show();
            Closing += MainWindow_Closing;

            dataManager = new(mainDatabase, secondaryDatabase);
            IConfiguration config = AppSettingsLoader.LoadConfiguration();
            ScrollViewer leftScrollViewer = LeftDataGrid.GetScrollViewer();
            ScrollViewer rightScrollViewer = RightDataGrid.GetScrollViewer();

            SetUpScrollSynchronization();

            LeftDataGrid.ChangeTitle(mainDatabase);
            LeftDataGrid.SelectionChanged += LeftDataGridSelectionChanged;
            LeftDataGrid.SetKnowledgePreviewBaseUrl(config["Urls:" + mainDatabase]!);

            RightDataGrid.IsRightDataGrid(true);
            RightDataGrid.ChangeTitle(secondaryDatabase);
            RightDataGrid.SelectionChanged += RightDataGridSelectionChanged;
            RightDataGrid.SetKnowledgePreviewBaseUrl(config["Urls:" + secondaryDatabase]!);

            ActionButtons.AutoSync += AutoSync;
            ActionButtons.ConfirmSync += ConfirmSync;
            ActionButtons.ManualSync += ManualSync;
            ActionButtons.DeleteSync += DeleteSync;
            ActionButtons.NewDatabases += ChooseNewDatabases;

            FilterPanel.FiltereChanged += VisualiseFilteredData;

            try
            {
                FilterPanel.LoadSubjects();
            }
            catch (SqlException)
            {
                Close();
                throw;
            }
        }

        private void LeftDataGridSelectionChanged(int selectedIndex)
        {
            if (mode == Mode.FilterData)
                FilterPanel.DataGridSelectionChanged(selectedIndex);
            else if (mode == Mode.ManualSync && FilterPanel.GetLayer() == Layer.Knowledge)
                LeftDataGrid.HandleKnowledgePreviews(selectedIndex);
            else if (mode == Mode.AutoSync && FilterPanel.GetLayer() != Layer.KnowledgeType)
            {
                LeftDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                RightDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
            }
        }

        private void RightDataGridSelectionChanged(int selectedIndex)
        {
            if (mode == Mode.ManualSync)
            {
                if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                    FilterPanel.DataGridSelectionChanged(selectedIndex);
                else if (LeftDataGrid.GetMetadata().GetLayer() == Layer.Knowledge)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
            }
            else if (mode == Mode.AutoSync)
            {
                if (FilterPanel.GetLayer() == Layer.KnowledgeType)
                    RightDataGrid.HandleKnowledgePreviews(selectedIndex);
                else
                {
                    LeftDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                    RightDataGrid.ChangeSynchronizationRowAcceptence(selectedIndex);
                }
            }
        }

        private async void VisualiseFilteredData(Filter filter)
        {
            FullData fullData;
            ActionButtons.loadingWindow.UpdateText("Načitava sa, prosím čakajte...");
            ActionButtons.loadingWindow.Show();

            try
            {
                fullData = await Task.Run(() => dataManager.GetFilterData(filter, mode));
            }
            catch (SqlException ex)
            {
                FilterPanel.SetPreviousLayer();
                ActionButtons.loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ActionButtons.loadingWindow.Hide();

            if (mode == Mode.AutoSync)
                ExitAutoSyncMode();

            if (mode != Mode.ManualSync)
            {
                LeftDataGrid.UpdateMetadata(fullData.LeftMetadata);
                LeftDataGrid.VisualizeDataGrid();
            }

            RightDataGrid.UpdateMetadata(fullData.RightMetadata);
            RightDataGrid.VisualizeDataGrid();

            FilterPanel.UpdateFilter(fullData.FilterData);
        }

        private void ManualSync()
        {
            if (mode == Mode.FilterData)
                EnterManualSyncMode();
            else
                ExitManualSyncMode();

            int leftColumn = Grid.GetColumn(FilterPanel);
            int rightColumn = Grid.GetColumn(ActionButtons);

            Grid.SetColumn(FilterPanel, rightColumn);
            Grid.SetColumn(ActionButtons, leftColumn);

            ActionButtons.ReverseButtons();
        }

        private void DeleteSync()
        {
            if (mode == Mode.AutoSync)
                DeleteAutoSync();
            else
                DeleteManualSync();
        }

        private void DeleteAutoSync()
        {
            MessageBoxResult cancle = MessageBox.Show("Naozaj chceš zrušiť AI synchronizáciu?",
                                                            "Potvrdenie zmazania", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (cancle == MessageBoxResult.Yes)
            {
                ExitAutoSyncMode();
                LeftDataGrid.ClearAISync();
                RightDataGrid.ClearAISync();
            }
        }

        private void DeleteManualSync()
        {
            int selectedIndex = FilterPanel.GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.GetSelectedIndex();

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
                dataManager.DeletePair(FilterPanel.GetLayer(), LeftDataGrid.GetMetadata().GetIdByRow(selectedIndex));
                RightDataGrid.GetMetadata().ClearRowData(selectedIndex);
            }
        }

        private void ChooseNewDatabases()
        {
            string message = "Naozaj chceš zmeniť databázy?";
            if (mode == Mode.AutoSync)
                message += "\nVšetky neuložené synchronizácie budú stratené!";

            MessageBoxResult change = MessageBox.Show(message, "Potvrdenie zmeny databáz", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (change == MessageBoxResult.Yes)
            {
                ConnectionWindow connectionWindow = new();
                connectionWindow.Show();
                Close();
            }
        }

        private async void AutoSync()
        {
            if (FilterPanel.GetLayer() == Layer.Subject)
            {
                MessageBox.Show("AI synchronizácia sa nedá použiť na predmety.\nPoužite manuálnu synchronizáciu.",
                                "Nemožná AI synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            //IsEnabled = false;
            //MyGridMetadata rightMetadata = new(FilterPanel.GetLayer(), true);
            int status = await Task.Run(() => dataManager.Synchronize(FilterPanel.GetFilter(), RightDataGrid.GetMetadata(), LeftDataGrid.GetMetadata()));

            ActionButtons.loadingWindow.Hide();

            if (status == -1)
                MessageBox.Show("Na synchronizovanie položiek v danom predmete je potrebné najprv synchronizovať daný predmet.",
                                "Synchronizujte predmet", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (status == 0)
                MessageBox.Show("Nenašla sa žiadna nová synchronizácia.",
                                "Žiadna synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                LeftDataGrid.SetAutoSyncMetadata(RightDataGrid.GetMetadata());
                RightDataGrid.SetAutoSyncMetadata(RightDataGrid.GetMetadata());
                EnterAutoSyncMode();
            //IsEnabled = true;
        }

        private void ConfirmSync()
        {
            if (mode == Mode.AutoSync)
                ConfirmAutoSync();
            else
                ConfirmManualSync();
        }

        private async void ConfirmAutoSync()
        {
            if (FilterPanel.GetLayer() == Layer.KnowledgeType)
            {
                if (RightDataGrid.GetSelectedIndex() == -1)
                {
                    MessageBox.Show("Na potvrdenie synchronizácie úlohy musíš označiť 1 položku v pravej tabuľke.",
                                    "Nevybraná synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ActionButtons.loadingWindow.UpdateText("Ukladá sa synchronizácia, prosím čakajte...");
                ActionButtons.loadingWindow.Show();
                await Task.Run(() => dataManager.SaveAISyncChanges(LeftDataGrid.GetMetadata(), RightDataGrid.GetMetadata(), RightDataGrid.GetSelectedIndex()));

                ExitAutoSyncMode();
                VisualiseFilteredData(FilterPanel.GetFilter());
            }
            else
            {
                ActionButtons.loadingWindow.UpdateText("Ukladajú sa synchronizácie, prosím čakajte...");
                ActionButtons.loadingWindow.Show();
                await Task.Run(() => dataManager.SaveAISyncChanges(LeftDataGrid.GetMetadata(), RightDataGrid.GetMetadata()));

                LeftDataGrid.ClearAISync();
                RightDataGrid.ClearAISync();
                ExitAutoSyncMode();
            }
        }

        private void ConfirmManualSync()
        {
            int leftIndex = LeftDataGrid.GetMetadata().GetLayer() == Layer.KnowledgeType ? 0 : LeftDataGrid.GetSelectedIndex();
            int rightIndex = LeftDataGrid.GetMetadata().GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.GetSelectedIndex();
            if (LeftDataGrid.GetMetadata().GetLayer() != RightDataGrid.GetMetadata().GetLayer())
                MessageBox.Show("Synchronizovať môžeš iba položky na rovnakej úrovni:\nPredmet-Predmet\nBalíček-Balíček\n...",
                                "Nesprávna úroveň", MessageBoxButton.OK, MessageBoxImage.Information);

            else if (leftIndex == -1 || rightIndex == -1)
                MessageBox.Show("Na synchronizovanie položiek musíš označiť 1 položku v ľavej tabuľke a 1 položku v pravej tabuľke.",
                                "Nevybrany pár", MessageBoxButton.OK, MessageBoxImage.Information);

            else if (LeftDataGrid.IsRowColor(leftIndex, SYNCED_COLOR) || RightDataGrid.IsRowColor(rightIndex, SYNCED_COLOR))
                MessageBox.Show("Vybraná položka už je synchronizovaná.\nVyberte položku, ktorá nie je synchronizovaná, alebo jej synchronizáciu zrušte.",
                                "Položka už je synchronizovaná", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                SynchronizePair(leftIndex, rightIndex);

            LeftDataGrid.SetSelectedIndex(-1);
            RightDataGrid.SetSelectedIndex(-1);
        }

        private void SynchronizePair(int leftIndex, int rightIndex)
        {
            LeftDataGrid.SetGridRowColor(leftIndex, SYNCED_COLOR);
            RightDataGrid.SetGridRowColor(rightIndex, SYNCED_COLOR);

            (int leftId, int rightId) = FilterPanel.GetIdsToSync(leftIndex, rightIndex);
            dataManager.SavePair(FilterPanel.GetLayer(), leftId, rightId);
        }

        private void EnterAutoSyncMode()
        {
            mode = Mode.AutoSync;
            ActionButtons.EnterAutoSyncMode();
        }

        private void ExitAutoSyncMode()
        {
            mode = Mode.FilterData;
            ActionButtons.EnterFilteringMode();
            LeftDataGrid.SetSelectedIndex(-1);
            RightDataGrid.SetSelectedIndex(-1);
        }

        private void EnterManualSyncMode()
        {
            mode = Mode.ManualSync;
            ActionButtons.EnterManualSyncMode();
            EnableScrollSynchronization(false);
            dataManager.VisualiseSyncedItems(LeftDataGrid.GetMetadata(), FilterPanel.GetFilter());
            LeftDataGrid.VisualizeDataGrid();
            FilterPanel.SwapFilters();
        }

        private void ExitManualSyncMode()
        {
            mode = Mode.FilterData;
            ActionButtons.EnterFilteringMode();
            EnableScrollSynchronization(true);
            FilterPanel.SwapFilters();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e) => ActionButtons.loadingWindow.Close();

        private void EnableScrollSynchronization(bool enable) => isScrollSynchronizationEnabled = enable;

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
            if (e.VerticalChange == 0 || !isScrollSynchronizationEnabled)
                return;

            if (sender == LeftDataGrid.GetScrollViewer())
                RightDataGrid.GetScrollViewer().ScrollToVerticalOffset(LeftDataGrid.GetScrollViewer().VerticalOffset);

            else
                LeftDataGrid.GetScrollViewer().ScrollToVerticalOffset(RightDataGrid.GetScrollViewer().VerticalOffset);
        }
    }
}