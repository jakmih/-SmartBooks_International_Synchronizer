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
        private readonly IConfiguration _config = AppSettingsLoader.LoadConfiguration();
        private readonly IEnumerable<string> _possibleConnections;
        private readonly LoadingWindow _loadingWindow = new();

        public ConnectionWindow()
        {

            InitializeComponent();
            Closing += ConnectionWindow_Closing;
            _possibleConnections = _config.GetRequiredSection("connectionStrings").GetChildren().Select(x => x.Key).Where(x => x != "Sync");
            MainDatabaseComboBox.ItemsSource = _possibleConnections;
        }

        public void MainDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            SecondaryDatabaseComboBox.IsEnabled = true;
            SecondaryDatabaseComboBox.SelectedItem = null;
            SecondaryDatabaseComboBox.ItemsSource = _possibleConnections.Where(x => x != MainDatabaseComboBox.SelectedValue.ToString());
        }

        public void SecondaryDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondaryDatabaseComboBox.SelectedValue != null)
                ConnectButton.IsEnabled = true;
        }

        public async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow? mainWindow = null;
            try
            {
                Hide();
                _loadingWindow.Show();
                mainWindow = await MainWindow.CreateAsync(MainDatabaseComboBox.SelectedValue.ToString()!, SecondaryDatabaseComboBox.SelectedValue.ToString()!);
                Close();
            }
            catch (SqlException ex)
            {
                _loadingWindow.Hide();
                mainWindow?.Close();
                Show();
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste to znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\n\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void ConnectionWindow_Closing(object? sender, CancelEventArgs e) => _loadingWindow.Close();
    }
}
