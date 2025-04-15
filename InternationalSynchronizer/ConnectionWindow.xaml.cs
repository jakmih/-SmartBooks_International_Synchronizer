using InternationalSynchronizer.Utilities;
using Microsoft.Extensions.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace InternationalSynchronizer
{
    public partial class ConnectionWindow : Window
    {
        private readonly IConfiguration config = AppSettingsLoader.LoadConfiguration();
        private readonly IEnumerable<string> possibleConnections;
        private readonly LoadingWindow loadingWindow = new();

        public ConnectionWindow()
        {
            InitializeComponent();
            loadingWindow.UpdateText("Aktualizujú sa položky v synchronizačnej databáze.");
            Closing += ConnectionWindow_Closing;
            possibleConnections = config.GetRequiredSection("connectionStrings").GetChildren().Select(x => x.Key).Where(x => x != "Sync");
            MainDatabaseComboBox.ItemsSource = possibleConnections;
        }

        public void MainDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            SecondaryDatabaseComboBox.IsEnabled = true;
            SecondaryDatabaseComboBox.SelectedItem = null;
            SecondaryDatabaseComboBox.ItemsSource = possibleConnections.Where(x => x != MainDatabaseComboBox.SelectedValue.ToString());
        }

        public void SecondaryDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondaryDatabaseComboBox.SelectedValue != null)
                ConnectButton.IsEnabled = true;
        }

        public void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow mainWindow = new(MainDatabaseComboBox.SelectedValue.ToString()!, SecondaryDatabaseComboBox.SelectedValue.ToString()!);
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste to znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            Close();
        }

        public void UpdateDatabasesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult update = MessageBox.Show("Naozaj chcete aktualizovať všetky databázy do synchronizačnej databázy?",
                                                      "Aktualizácia databáz", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (update != MessageBoxResult.Yes)
                return;

            Dictionary<string, string> databasesDictionary = [];
            foreach (string database in possibleConnections)
                databasesDictionary.Add(database, config.GetConnectionString(database)!);

            Databases databases = new(config.GetConnectionString("Sync")!, databasesDictionary);

            UpdateDatabases(databases);
        }

        private async void UpdateDatabases(Databases databases)
        {
            IsEnabled = false;
            loadingWindow.Show();

            SqlException? exception = null;
            int updated = await Task.Run(() =>
            {
                try
                {
                    return databases.Update();
                }
                catch (SqlException ex)
                {
                    exception = ex;
                    return -1;
                }
            });

            loadingWindow.Hide();
            IsEnabled = true;

            if (updated == -1)
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste to znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + exception!.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (updated == 0)
                MessageBox.Show("Nenašli sa žiadné nové položky.\nSynchronizačná databáza je aktuálna.",
                                "Aktualizácia databáz", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"Úspešne aktualizované.\nPočet položiek pridaných do synchronizačnej databázy: {updated}",
                                "Aktualizácia databáz", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConnectionWindow_Closing(object? sender, CancelEventArgs e) => loadingWindow.Close();
    }
}
