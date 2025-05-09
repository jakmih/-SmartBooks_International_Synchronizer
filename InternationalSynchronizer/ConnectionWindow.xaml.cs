using Microsoft.Extensions.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace InternationalSynchronizer
{
    public partial class ConnectionWindow : Window
    {
        private readonly LoadingWindow _loadingWindow = new();
        private readonly Dictionary<Int32, string> _titles;

        public ConnectionWindow()
        {
            InitializeComponent();
            Closing += ConnectionWindow_Closing;
            _titles = App.Config.GetRequiredSection("Titles").GetChildren().ToDictionary(x => Int32.Parse(x.Key), x => x.Value!);
            MainDatabaseComboBox.ItemsSource = _titles.ToList();
            MainDatabaseComboBox.DisplayMemberPath = "Value";
            MainDatabaseComboBox.SelectedValuePath = "Key";
        }

        public void MainDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Int32 selectedKey = (Int32)MainDatabaseComboBox.SelectedValue;
            ConnectButton.IsEnabled = false;
            SecondaryDatabaseComboBox.IsEnabled = true;
            SecondaryDatabaseComboBox.SelectedItem = null;
            SecondaryDatabaseComboBox.ItemsSource = _titles.Where(kv => kv.Key != selectedKey).ToList();
            SecondaryDatabaseComboBox.DisplayMemberPath = "Value";
            SecondaryDatabaseComboBox.SelectedValuePath = "Key";
        }

        public void SecondaryDatabaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondaryDatabaseComboBox.SelectedValue != null)
                ConnectButton.IsEnabled = true;
        }

        public async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow? mainWindow = null;
            Int32 mainKey = (Int32)MainDatabaseComboBox.SelectedValue;
            Int32 secondaryKey = (Int32)SecondaryDatabaseComboBox.SelectedValue;
            Debug.WriteLine($"Selected: {mainKey} {secondaryKey}");
            try
            {
                Hide();
                _loadingWindow.Show();
                mainWindow = await MainWindow.CreateAsync(mainKey, secondaryKey);
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
