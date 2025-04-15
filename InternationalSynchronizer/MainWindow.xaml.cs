using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static InternationalSynchronizer.Utilities.SqlQuery;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer
{
    public partial class MainWindow : Window
    {
        private readonly LoadingWindow loadingWindow = new();
        private DataGridMetadata leftSideMetadata;
        private DataGridMetadata rightSideMetadata;
        private readonly ItemCache itemCache;
        private Filter filter = new();
        private Filter filterStorage = new();
        private bool loadingComboBoxes = false;
        private Mode mode = Mode.FilterData;
        private readonly IConfiguration config = AppSettingsLoader.LoadConfiguration();
        private readonly Synchronizer synchronizer;

        public MainWindow(string mainDatabase, string secondaryDatabase)
        {
            InitializeComponent();
            VisualiseButtons();
            Show();
            Closing += MainWindow_Closing;

            string mainConnectionString = config.GetConnectionString(mainDatabase)!;
            string secondaryConnectionString = config.GetConnectionString(secondaryDatabase)!;
            string leftBaseUrl = config.GetRequiredSection("Urls")[mainDatabase]!;
            string rightBaseUrl = config.GetRequiredSection("Urls")[secondaryDatabase]!;
            ScrollViewer leftScrollViewer = GetScrollViewer(LeftDataGrid)!;
            ScrollViewer rightScrollViewer = GetScrollViewer(RightDataGrid)!;
            leftSideMetadata = new(leftScrollViewer, mainConnectionString, leftBaseUrl);
            rightSideMetadata = new(rightScrollViewer, secondaryConnectionString, rightBaseUrl);

            itemCache = new(mainConnectionString, secondaryConnectionString);
            synchronizer = new(config.GetConnectionString("Sync")!, mainDatabase, secondaryDatabase);

            EnableScrollSynchronization(true);
            try
            {
                LoadSubjects();
            }
            catch (SqlException)
            {
                Close();
                throw;
            }
            LeftDataGridText.Text = mainDatabase;
            RightDataGridText.Text = secondaryDatabase;
            filterStorage.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
        }

        private void LoadSubjects()
        {
            BackButton.IsEnabled = false;
            LoadFilteredData(Layer.Subject);
        }

        private void SubjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubjectComboBox.SelectedItem != null)
            {
                BackButton.IsEnabled = true;
                LoadFilteredData(Layer.Package);
            }
        }

        private void PackageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PackageComboBox.SelectedItem != null)
                LoadFilteredData(Layer.Theme);
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem != null)
                LoadFilteredData(Layer.Knowledge);
        }

        private void KnowledgeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KnowledgeComboBox.SelectedItem != null)
                LoadFilteredData(Layer.KnowledgeType);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid dataGrid || dataGrid.SelectedItem == null)
                return;

            switch (filter.GetLayer())
            {
                case Layer.Subject:
                    SubjectComboBox.SelectedIndex = dataGrid.SelectedIndex;
                    break;
                case Layer.Package:
                    PackageComboBox.SelectedIndex = dataGrid.SelectedIndex;
                    break;
                case Layer.Theme:
                    ThemeComboBox.SelectedIndex = dataGrid.SelectedIndex;
                    break;
                case Layer.Knowledge:
                    KnowledgeComboBox.SelectedIndex = dataGrid.SelectedIndex;
                    break;
                case Layer.KnowledgeType:
                    break;
            }
        }

        private void LoadFilteredData(Layer layer)
        {
            if (loadingComboBoxes)
                return;

            EnableComboBox(layer);
            filter.SetLayer(layer);
            ComboBox? currentComboBox = GetCombobox(layer);
            ComboBox? previousComboBox = GetCombobox(layer - 1);

            if (previousComboBox != null)
                filter.SetLayerId(previousComboBox.SelectedIndex);

            HandleKnowledgePreviews();

            while (true) try
            {
                List<string> filterData = SetDataGridsAndGetFilterData();
                if (currentComboBox != null)
                    currentComboBox.ItemsSource = filterData;
                break;
            }
            catch (SqlException ex)
            {
                if (layer == Layer.Subject)
                    throw;
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ComboBox? GetCombobox(Layer layer)
        {
            return layer switch
            {
                Layer.Subject => SubjectComboBox,
                Layer.Package => PackageComboBox,
                Layer.Theme => ThemeComboBox,
                Layer.Knowledge => KnowledgeComboBox,
                _ => null,
            };
        }

        private void EnableComboBox(Layer layer)
        {
            SubjectComboBox.IsEnabled = true;
            PackageComboBox.IsEnabled = true;
            ThemeComboBox.IsEnabled = true;
            KnowledgeComboBox.IsEnabled = true;
            if (layer != Layer.KnowledgeType)
            {
                KnowledgeComboBox.SelectedItem = null;
                if (layer != Layer.Knowledge)
                {
                    KnowledgeComboBox.IsEnabled = false;
                    ThemeComboBox.SelectedItem = null;
                    if (layer != Layer.Theme)
                    {
                        ThemeComboBox.IsEnabled = false;
                        PackageComboBox.SelectedItem = null;
                        if (layer != Layer.Package)
                        {
                            PackageComboBox.IsEnabled = false;
                            SubjectComboBox.SelectedItem = null;
                        }
                    }
                }
            }
        }

        private List<string> SetDataGridsAndGetFilterData()
        {
            rightSideMetadata.table = NewDataTable(true);
            rightSideMetadata.rowColors = [];
            List<string> filterData = [];

            if (mode != Mode.ManualSync)
            {
                leftSideMetadata.table = NewDataTable(false);
                leftSideMetadata.rowColors = [];
                SetDataTable(leftSideMetadata.table, leftSideMetadata.connectionString, filterData);
                SetSynchonizedDataTable();
                VisualiseDataTable(LeftDataGrid);
            }
            else
            {
                SetDataTable(rightSideMetadata.table, rightSideMetadata.connectionString, filterData);
                if (filter.GetLayer() == filterStorage.GetLayer())
                {
                    RightDataGrid.SelectionChanged -= DataGrid_SelectionChanged;
                    RightDataGrid.SelectionMode = DataGridSelectionMode.Single;
                }
                else
                {
                    RightDataGrid.SelectionChanged -= DataGrid_SelectionChanged;
                    RightDataGrid.SelectionChanged += DataGrid_SelectionChanged;
                    RightDataGrid.SelectionMode = DataGridSelectionMode.Extended;
                }
                VisualiseSyncedItems(RightDataGrid);
            }
            VisualiseDataTable(RightDataGrid);

            if (mode == Mode.AutoSync)
                ExitAutoSyncMode();

            return filterData;
        }

        private void SetDataTable(DataTable table, string connectionString, List<string> filterData)
        {
            List<Int32> ids = [];
            string query = GetItemQuery(filter.GetLayer(), filter.GetUpperLayerId(), true);
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(query, connection);

            connection.Open();
            using var mainReader = command.ExecuteReader();

            while (mainReader.Read())
            {
                string[] row = ExtractRowData(mainReader, connectionString == rightSideMetadata.connectionString, ids);
                if (row.IsNullOrEmpty())
                    continue;

                filterData.Add(table.Equals(leftSideMetadata.table) ? row[^1] : row[0]);

                table.Rows.Add(row);
                if (table.Equals(leftSideMetadata.table))
                    leftSideMetadata.rowColors.Add(NEUTRAL_COLOR);
                else
                    rightSideMetadata.rowColors.Add(NEUTRAL_COLOR);
            }
            filter.SetIds(ids);
        }

        private void SetSynchonizedDataTable()
        {
            foreach (Int32 id in synchronizer.GetSynchronizedIds(filter))
            {
                rightSideMetadata.rowColors.Add(NEUTRAL_COLOR);

                if (id == -1 || !TryGetRightRow(id, out List<string> rightRow))
                    rightSideMetadata.table.Rows.Add(new object[rightSideMetadata.table.Columns.Count]);
                else
                    rightSideMetadata.table.Rows.Add(rightRow.ToArray());
            }
        }

        private DataTable NewDataTable(bool reversed)
        {
            if (reversed)
                rightSideMetadata.rowColors = [];
            else
                leftSideMetadata.rowColors = [];

            DataTable table = new();
            List<string> columns = ["Predmet"];
            if (filter.GetLayer() != Layer.Subject)
            {
                columns.Add("Balíček");
                if (filter.GetLayer() != Layer.Package)
                {
                    columns.Add("Téma");
                    if (filter.GetLayer() != Layer.Theme)
                    {
                        columns.Add("Pod-Téma");
                        columns.Add("Úloha");
                        if (filter.GetLayer() != Layer.Knowledge)
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

        private string[] ExtractRowData(SqlDataReader reader, bool mirrorRowData, List<Int32> ids)
        {
            List<string> rowData = [];
            for (int i = 0; i < reader.FieldCount - 1; i++)
                rowData.Add(reader.IsDBNull(i) ? "" : reader.GetString(i).Replace('\n', ' ').Trim());

            if ((rowData[^1].Equals("") && filter.GetLayer() != Layer.KnowledgeType) || rowData[^1].StartsWith("*IMPORT*"))
                return [];

            ids.Add(reader.GetInt32(reader.FieldCount - 1));

            if (mirrorRowData)
                rowData.Reverse();

            return rowData.ToArray();
        }

        private bool TryGetRightRow(int id, out List<string> row)
        {
            row = new(itemCache.GetItem(filter.GetLayer(), id));

            if (row.IsNullOrEmpty())
                return false;

            row.Reverse();
            return true;
        }

        public void VisualiseDataTable(DataGrid grid)
        {
            grid.ItemsSource = (grid.Equals(RightDataGrid) ? rightSideMetadata.table : leftSideMetadata.table).DefaultView;

            if (grid.Items.Count > 0)
                grid.ScrollIntoView(grid.Items[0], grid.Columns[grid.Equals(LeftDataGrid) ? ^1 : 0]);

            grid.UpdateLayout();
            grid.Columns[^1].MinWidth = grid.Columns[^1].Width.DisplayValue;
            grid.Columns[^1].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        }

        private void VisualiseSyncedItems(DataGrid grid)
        {
            int index = 0;
            foreach (Int32 id in synchronizer.GetSynchronizedIds(filter, grid.Equals(RightDataGrid)))
            {
                if (id != -1)
                    SetGridRowColor(grid, index, SYNCED_COLOR);
                index++;
            }
        }

        private void EditRows(List<List<string>> rows)
        {
            if (filter.GetLayer() == Layer.KnowledgeType)
            {
                rightSideMetadata.table = NewDataTable(true);
                foreach (var row in rows)
                {
                    rightSideMetadata.rowColors.Add(ACCEPT_COLOR);
                    rightSideMetadata.table.Rows.Add(row.ToArray());
                }

                return;
            }

            DataTable newRightTable = NewDataTable(true);
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rows[rowIndex].IsNullOrEmpty())
                {
                    newRightTable.Rows.Add(rightSideMetadata.table.Rows[rowIndex].ItemArray);
                    rightSideMetadata.rowColors.Add(NEUTRAL_COLOR);
                    continue;
                }

                SetGridRowColor(LeftDataGrid, rowIndex, ACCEPT_COLOR);
                rightSideMetadata.rowColors.Add(ACCEPT_COLOR);
                newRightTable.Rows.Add(rows[rowIndex].ToArray());
            }
            rightSideMetadata.table = newRightTable;
        }

        private void SynchronizePair()
        {
            int leftIndex = filter.GetLayer() == Layer.KnowledgeType ? 0 : LeftDataGrid.SelectedIndex;
            int rightIndex = filter.GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.SelectedIndex;
            SetGridRowColor(LeftDataGrid, leftIndex, SYNCED_COLOR);
            SetGridRowColor(RightDataGrid, rightIndex, SYNCED_COLOR);
            synchronizer.SavePair(filter.GetLayer(),
                                  filterStorage.GetIdByRow(leftIndex),
                                  filter.GetIdByRow(rightIndex));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (filter.GetLayer() == Layer.Subject)
                return;

            switch (filter.GetLayer())
            {
                case Layer.Package:
                    SubjectComboBox.SelectedItem = null;
                    try
                    {
                        LoadSubjects();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                        "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case Layer.Theme:
                    PackageComboBox.SelectedItem = null;
                    LoadFilteredData(Layer.Package);
                    break;
                case Layer.Knowledge:
                    ThemeComboBox.SelectedItem = null;
                    LoadFilteredData(Layer.Theme);
                    break;
                case Layer.KnowledgeType:
                    KnowledgeComboBox.SelectedItem = null;
                    LoadFilteredData(Layer.Knowledge);
                    break;
            }
        }

        private async void AutoSyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (filter.GetLayer() == Layer.Subject)
            {
                MessageBox.Show("AI synchronizácia sa nedá použiť na predmety.\nPoužite manuálnu synchronizáciu.",
                                "Nemožná AI synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsEnabled = false;
            SqlException? exception;
            List<List<string>> rows;
            do
            {
                exception = null;
                loadingWindow.Show();
                rows = await Task.Run(() =>
                {
                    try
                    {
                        return synchronizer.Synchronize(filter, itemCache, LeftDataGrid);
                    }
                    catch (SqlException ex)
                    {
                        exception = ex;
                        return [];
                    }
                });
                loadingWindow.Hide();
            }
            while (!AutoSynced(exception, rows));
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            while (true) try
            {
                if (mode == Mode.AutoSync)
                    ConfirmAutoSync();

                else if (mode == Mode.ManualSync)
                    ConfirmManualSync();

                break;
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void ManualSyncButton_Click(object sender, RoutedEventArgs e)
        {
            while (true) try
            {
                if (mode == Mode.FilterData)
                    EnterManualSyncMode();
                else
                    ExitManualSyncMode();

                break;
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            int leftColumn = Grid.GetColumn(LeftControls);
            int rightColumn = Grid.GetColumn(RightControls);

            Grid.SetColumn(LeftControls, rightColumn);
            Grid.SetColumn(RightControls, leftColumn);

            ReverseButtons(RightControlsPanel);
        }

        private void DeleteSyncButton_Click(object sender, RoutedEventArgs e)
        {
            Mode modeTmp = mode;
            while (true) try
            {
                if (mode == Mode.AutoSync)
                {
                    MessageBoxResult cancle = MessageBox.Show("Naozaj chceš zrušiť synchronizáciu?",
                                                              "Potvrdenie zmazania", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (cancle == MessageBoxResult.Yes)
                    {
                        ExitAutoSyncMode();
                        LoadFilteredData(filter.GetLayer());
                    }
                    return;
                }

                int selectedIndex = filter.GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.SelectedIndex;

                if (selectedIndex == -1
                    || synchronizer.GetSynchronizedIds(filter, id: filter.GetIdByRow(selectedIndex))[0] == -1)
                {
                    MessageBox.Show("Na zrušenie synchronizácie položiek musíš označiť 1 synchronizovanú položku v pravej tabuľke.",
                                    "Nevybrany pár", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBoxResult delete = MessageBox.Show("Naozaj chceš odstrániť túto synchronizáciu?",
                                                          "Potvrdenie zmazania", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (delete == MessageBoxResult.Yes)
                {
                    synchronizer.DeletePair(filter.GetLayer(), filter.GetIdByRow(selectedIndex));
                    RemoveRightRow(selectedIndex);
                    HandleKnowledgePreviews();
                }
                break;
            }
            catch (SqlException ex)
            {
                mode = modeTmp;
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchDatabasesButton_Click(object sender, RoutedEventArgs e)
        {
            (LeftDataGridText.Text, RightDataGridText.Text) = (RightDataGridText.Text, LeftDataGridText.Text);
            (leftSideMetadata, rightSideMetadata) = (rightSideMetadata, leftSideMetadata);
            leftSideMetadata.ReverseTable();
            synchronizer.SwitchDatabases();
            itemCache.Switch();
            Mode modeTmp = mode;
            while (true) try
            {
                SwapFilters(mode == Mode.ManualSync);
                break;
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                mode = modeTmp;
            }
        }

        private void ChooseNewDatabasesButton_Click(object sender, RoutedEventArgs e)
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

        private bool AutoSynced(SqlException? exception, List<List<string>> rows)
        {
            if (exception != null)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + exception.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            switch (rows)
            {
                case []:
                    MessageBox.Show("Na synchronizovanie položiek v danom predmete je potrebné najprv synchronizovať daný predmet.",
                                    "Synchronizujte predmet", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case [[]]:
                    MessageBox.Show("Nenašla sa žiadna nová synchronizácia.",
                                    "Žiadna synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    EditRows(rows);
                    VisualiseDataTable(RightDataGrid);
                    EnterAutoSyncMode();
                    break;
            }
            IsEnabled = true;
            return true;
        }

        private void ConfirmAutoSync()
        {
            if (filter.GetLayer() == Layer.KnowledgeType)
            {
                if (RightDataGrid.SelectedIndex == -1)
                {
                    MessageBox.Show("Na potvrdenie synchronizácie úlohy musíš označiť 1 položku v pravej tabuľke.",
                                    "Nevybraná synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                synchronizer.SaveChanges(filter, leftSideMetadata.rowColors, RightDataGrid.SelectedIndex);
                ExitAutoSyncMode();
                LoadFilteredData(Layer.KnowledgeType);
            }
            else
            {
                synchronizer.SaveChanges(filter, leftSideMetadata.rowColors);

                for (int i = 0; i < LeftDataGrid.Items.Count; i++)
                    if (leftSideMetadata.rowColors[i].Equals(DECLINE_COLOR))
                        RemoveRightRow(i);

                ClearRowsColor();
                ExitAutoSyncMode();
            }
        }

        private void ConfirmManualSync()
        {
            int leftIndex = filter.GetLayer() == Layer.KnowledgeType ? 0 : LeftDataGrid.SelectedIndex;
            int rightIndex = filter.GetLayer() == Layer.KnowledgeType ? 0 : RightDataGrid.SelectedIndex;
            if (filter.GetLayer() != filterStorage.GetLayer())
                MessageBox.Show("Synchronizovať môžeš iba položky na rovnakej úrovni:\nPredmet-Predmet\nBalíček-Balíček\n...",
                                "Nesprávna úroveň", MessageBoxButton.OK, MessageBoxImage.Information);

            else if (leftIndex == -1 || rightIndex == -1)
                MessageBox.Show("Na synchronizovanie položiek musíš označiť 1 položku v ľavej tabuľke a 1 položku v pravej tabuľke.",
                                "Nevybrany pár", MessageBoxButton.OK, MessageBoxImage.Information);

            else if (LeftDataGrid.ItemContainerGenerator.ContainerFromIndex(leftIndex) is DataGridRow left
                    && left.Background == SYNCED_COLOR
                    || RightDataGrid.ItemContainerGenerator.ContainerFromIndex(rightIndex) is DataGridRow right
                    && right.Background == SYNCED_COLOR)
                MessageBox.Show("Vybraná položka už je synchronizovaná.\nVyberte položku, ktorá nie je synchronizovaná, alebo jej synchronizáciu zrušte.",
                                "Položka už je synchronizovaná", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                SynchronizePair();

            LeftDataGrid.SelectedIndex = -1;
            RightDataGrid.SelectedIndex = -1;
        }

        private void EnterAutoSyncMode()
        {
            mode = Mode.AutoSync;
            BackButton.IsEnabled = false;
            AutoSyncButton.IsEnabled = false;
            ConfirmButton.IsEnabled = true;
            ManualSyncButton.IsEnabled = false;
            SwitchDatabasesButton.IsEnabled = false;
            DeleteSyncButton.Content = "Zrušiť synchronizáciu";
            LeftDataGrid.SelectionChanged -= DataGrid_SelectionChanged;
            LeftDataGrid.SelectionChanged += DataGrid_ChangeSynchronizationRowAcceptence;
            LeftDataGrid.SelectionMode = DataGridSelectionMode.Single;
            if (filter.GetLayer() == Layer.KnowledgeType)
                RightDataGrid.SelectionChanged += DataGrid_KnowledgeSelected;
            else
                RightDataGrid.SelectionChanged += DataGrid_ChangeSynchronizationRowAcceptence;
        }

        private void ExitAutoSyncMode()
        {
            mode = Mode.FilterData;
            BackButton.IsEnabled = true;
            AutoSyncButton.IsEnabled = true;
            ConfirmButton.IsEnabled = false;
            ManualSyncButton.IsEnabled = true;
            SwitchDatabasesButton.IsEnabled = true;
            DeleteSyncButton.Content = "Odstrániť synchronizáciu";
            LeftDataGrid.SelectionChanged -= DataGrid_ChangeSynchronizationRowAcceptence;
            LeftDataGrid.SelectionChanged += DataGrid_SelectionChanged;
            LeftDataGrid.SelectionMode = DataGridSelectionMode.Extended;
            LeftDataGrid.SelectedIndex = -1;
            RightDataGrid.SelectionChanged -= DataGrid_KnowledgeSelected;
            RightDataGrid.SelectionChanged -= DataGrid_ChangeSynchronizationRowAcceptence;
            RightDataGrid.SelectedIndex = -1;
        }

        private void EnterManualSyncMode()
        {
            mode = Mode.ManualSync;
            AutoSyncButton.Visibility = Visibility.Collapsed;
            ConfirmButton.IsEnabled = true;
            DeleteSyncButton.Visibility = Visibility.Collapsed;
            ManualSyncButton.Content = "Vypnúť ručnú synchronizáciu";
            LeftDataGrid.SelectionChanged -= DataGrid_SelectionChanged;
            RightDataGrid.SelectionChanged += DataGrid_SelectionChanged;
            LeftDataGrid.SelectionMode = DataGridSelectionMode.Single;
            RightDataGrid.SelectionMode = DataGridSelectionMode.Extended;
            EnableScrollSynchronization(false);
            VisualiseSyncedItems(LeftDataGrid);
            SwapFilters();
        }

        private void ExitManualSyncMode()
        {
            mode = Mode.FilterData;
            AutoSyncButton.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
            DeleteSyncButton.Visibility = Visibility.Visible;
            ManualSyncButton.Content = "Zapnúť ručnú synchronizáciu";
            LeftDataGrid.SelectionChanged += DataGrid_SelectionChanged;
            RightDataGrid.SelectionChanged -= DataGrid_SelectionChanged;
            LeftDataGrid.SelectionMode = DataGridSelectionMode.Extended;
            RightDataGrid.SelectionMode = DataGridSelectionMode.Single;
            EnableScrollSynchronization(true);
            SwapFilters();
        }

        private void SwapFilters(bool swappingManualMode = false)
        {
            filter.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
            (filterStorage, filter) = (filter, filterStorage);
            LoadComboBoxes();

            if (swappingManualMode)
                VisualiseDataTable(LeftDataGrid);

            LoadFilteredData(filter.GetLayer());
        }

        private void ReverseButtons(StackPanel panel)
        {
            List<UIElement> children = panel.Children.Cast<UIElement>().ToList();
            panel.Children.Clear();
            foreach (UIElement child in children.Reverse<UIElement>())
                panel.Children.Add(child);
        }

        private void DataGrid_ChangeSynchronizationRowAcceptence(object sender, SelectionChangedEventArgs e)
        {
            int index = ((DataGrid)sender).SelectedIndex;

            if (index == -1 || filter.GetLayer() == Layer.KnowledgeType)
                return;

            DataGridRow left = (DataGridRow)LeftDataGrid.ItemContainerGenerator.ContainerFromIndex(index);
            DataGridRow right = (DataGridRow)RightDataGrid.ItemContainerGenerator.ContainerFromIndex(index);

            if (left.Background == ACCEPT_COLOR || left.Background == DECLINE_COLOR)
            {
                leftSideMetadata.rowColors[index] = leftSideMetadata.rowColors[index] == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                rightSideMetadata.rowColors[index] = rightSideMetadata.rowColors[index] == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                left.Background = left.Background == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
                right.Background = right.Background == ACCEPT_COLOR ? DECLINE_COLOR : ACCEPT_COLOR;
            }

            RightDataGrid.SelectedIndex = -1;
            LeftDataGrid.SelectedIndex = -1;
        }

        private void DataGrid_KnowledgeSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid dataGrid || dataGrid.SelectedIndex == -1)
                return;

            LoadKnowledgePreviews(synchronizer.GetIdByRow(dataGrid.SelectedIndex), true);
        }

        private void LoadKnowledgePreviews(Int32 id, bool right)
        {
            RowDefinition knowledgePreviewsRow = (RowDefinition)FindName("KnowledgePreviews");
            knowledgePreviewsRow.Height = new GridLength(1, GridUnitType.Star);

            string pass = "&password=<REDACTED>";
            string knowledgePath = "/extern_knowledge_preview?knowledgeID=";

            if (right)
                RightWebView.Source = new Uri(rightSideMetadata.baseUrl + knowledgePath + id + pass);
            else
                LeftWebView.Source = new Uri(leftSideMetadata.baseUrl + knowledgePath + id + pass);
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (sender.Equals(RightDataGrid) && e.Row.GetIndex() >= 0 && e.Row.GetIndex() < rightSideMetadata.rowColors.Count)
                e.Row.Background = rightSideMetadata.rowColors[e.Row.GetIndex()];
            else if (sender.Equals(LeftDataGrid) && e.Row.GetIndex() >= 0 && e.Row.GetIndex() < leftSideMetadata.rowColors.Count)
                e.Row.Background = leftSideMetadata.rowColors[e.Row.GetIndex()];
        }

        private void SetGridRowColor(DataGrid grid, Int32 index, SolidColorBrush color)
        {
            if (grid.Equals(LeftDataGrid))
                leftSideMetadata.rowColors[index] = color;
            else
                rightSideMetadata.rowColors[index] = color;

            if (grid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
                row.Background = color;
        }

        private void RemoveRightRow(int index)
        {
            DataRowView row = (DataRowView)RightDataGrid.Items[index];
            row.BeginEdit();

            for (int i = 0; i < row.Row.ItemArray.Length; i++)
                row[i] = "";

            row.EndEdit();
        }

        private void ClearRowsColor()
        {
            for (int i = 0; i < leftSideMetadata.rowColors.Count; i++)
                leftSideMetadata.rowColors[i] = NEUTRAL_COLOR;

            for (int i = 0; i < rightSideMetadata.rowColors.Count; i++)
                rightSideMetadata.rowColors[i] = NEUTRAL_COLOR;

            foreach (var item in LeftDataGrid.Items)
                if (LeftDataGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                    row.Background = NEUTRAL_COLOR;

            foreach (var item in RightDataGrid.Items)
                if (RightDataGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                    row.Background = NEUTRAL_COLOR;
        }

        private void LoadComboBoxes()
        {
            loadingComboBoxes = true;
            List<ComboBox> comboBoxes = [SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox];

            int i = 0;
            foreach (var itemSource in filter.GetItemSources())
                if (i != (int)filter.GetLayer())
                    comboBoxes[i++].ItemsSource = itemSource;

            i = 0;
            foreach (var selectedItem in filter.GetSelectedItems())
                if (i != (int)filter.GetLayer())
                    comboBoxes[i++].SelectedItem = selectedItem;

            loadingComboBoxes = false;
        }

        private void HandleKnowledgePreviews()
        {
            if (mode == Mode.ManualSync && (filter.GetLayer() == Layer.KnowledgeType || filterStorage.GetLayer() == Layer.KnowledgeType))
            {
                LoadKnowledgePreviews(filter.GetLayer() == Layer.KnowledgeType ? filter.GetKnowledgeId() : 0, true);
                LoadKnowledgePreviews(filterStorage.GetLayer() == Layer.KnowledgeType ? filterStorage.GetKnowledgeId() : 0, false);
                return;
            }

            if (filter.GetLayer() != Layer.KnowledgeType)
            {
                RowDefinition KnowledgePreviewsRow = (RowDefinition)FindName("KnowledgePreviews");
                KnowledgePreviewsRow.Height = new GridLength(0);
                return;
            }

            LoadKnowledgePreviews(filter.GetKnowledgeId(), false);
            LoadKnowledgePreviews(synchronizer.GetSynchronizedIds(filter)[0], true);
        }

        private void VisualiseButtons()
        {
            BackButton.Background = BACK_BUTTON_COLOR;
            AutoSyncButton.Background = AUTO_SYNC_BUTTON_COLOR;
            ConfirmButton.Background = CONFIRM_BUTTON_COLOR;
            ManualSyncButton.Background = MANUAL_SYNC_BUTTON_COLOR;
            DeleteSyncButton.Background = DELETE_SYNC_BUTTON_COLOR;
            SwitchDatabasesButton.Background = SWITCH_DATABASES_BUTTON_COLOR;
            ChooseNewDatabasesButton.Background = CHOOSE_NEW_DATABASES_BUTTON_COLOR;
        }

        private void EnableScrollSynchronization(bool enable)
        {
            if (enable)
            {
                leftSideMetadata.scrollViewer.ScrollChanged += ScrollChanged;
                rightSideMetadata.scrollViewer.ScrollChanged += ScrollChanged;
            }
            else
            {
                leftSideMetadata.scrollViewer.ScrollChanged -= ScrollChanged;
                rightSideMetadata.scrollViewer.ScrollChanged -= ScrollChanged;
            }
        }

        private void ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0)
                return;

            if (sender == leftSideMetadata.scrollViewer)
                rightSideMetadata.scrollViewer.ScrollToVerticalOffset(leftSideMetadata.scrollViewer.VerticalOffset);

            else
                leftSideMetadata.scrollViewer.ScrollToVerticalOffset(rightSideMetadata.scrollViewer.VerticalOffset);
        }

        private ScrollViewer? GetScrollViewer(DependencyObject grid)
        {
            if (grid is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e) => loadingWindow.Close();
        private class DataGridMetadata(ScrollViewer leftScrollViewer, string connectionString, string baseUrl)
        {
            public List<SolidColorBrush> rowColors = [];
            public ScrollViewer scrollViewer = leftScrollViewer;
            public DataTable table = new();
            public string connectionString = connectionString;
            public string baseUrl = baseUrl;

            public void ReverseTable()
            {
                DataTable reversedTable = new();

                for (int i = table.Columns.Count - 1; i >= 0; i--)
                {
                    DataColumn originalColumn = table.Columns[i];
                    reversedTable.Columns.Add(originalColumn.ColumnName, originalColumn.DataType);
                }

                foreach (DataRow row in table.Rows)
                {
                    object[] reversedData = new object[table.Columns.Count];
                    for (int i = 0; i < table.Columns.Count; i++)
                        reversedData[i] = row[table.Columns[table.Columns.Count - 1 - i]];

                    reversedTable.Rows.Add(reversedData);
                }

                table = reversedTable;
            }
        }
    }
}