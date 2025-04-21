using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Components
{
    public partial class ActionButtons : UserControl
    {
        public readonly LoadingWindow loadingWindow = new();
        public ActionButtons()
        {
            InitializeComponent();
            VisualiseButtons();
        }

        public event Action? AutoSync;
        public event Action? ConfirmSync;
        public event Action? ManualSync;
        public event Action? DeleteSync;
        public event Action? NewDatabases;

        private void AutoSyncButton_Click(object sender, RoutedEventArgs e)
        {
            loadingWindow.UpdateText("Hľadajú sa nové synchronizácie, prosím čakajte...");
            loadingWindow.Show();

            try
            {
                AutoSync?.Invoke();
                loadingWindow.Hide();
            }
            catch (SqlException exception)
            {
                loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + exception.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfirmSync?.Invoke();
                loadingWindow.Hide();
            }
            catch (SqlException ex)
            {
                loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManualSyncButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ManualSync?.Invoke();
                loadingWindow.Hide();
            }
            catch (SqlException ex)
            {
                loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeleteSync is null)
            {
                Debug.WriteLine("DeleteSync is null, you should set it first.");
                return;
            }

            try
            {
                DeleteSync?.Invoke();
                loadingWindow.Hide();
            }
            catch (SqlException ex)
            {
                loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a stlačte 'ok'. Pokiaľ problem pretrváva, kontaktujte Vedenie.\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChooseNewDatabasesButton_Click(object sender, RoutedEventArgs e)
        {
            NewDatabases?.Invoke();
        }

        public void EnterAutoSyncMode()
        {
            AutoSyncButton.IsEnabled = false;
            ConfirmButton.IsEnabled = true;
            ManualSyncButton.IsEnabled = false;

            DeleteSyncButton.Content = "Zrušiť synchronizáciu";
        }

        public void EnterFilteringMode()
        {
            AutoSyncButton.IsEnabled = true;
            ConfirmButton.IsEnabled = false;
            ManualSyncButton.IsEnabled = true;

            AutoSyncButton.Visibility = Visibility.Visible;
            DeleteSyncButton.Visibility = Visibility.Visible;

            DeleteSyncButton.Content = "Odstrániť synchronizáciu";
            ManualSyncButton.Content = "Zapnúť ručnú synchronizáciu";
        }

        public void EnterManualSyncMode()
        {
            ConfirmButton.IsEnabled = true;

            AutoSyncButton.Visibility = Visibility.Collapsed;
            DeleteSyncButton.Visibility = Visibility.Collapsed;

            ManualSyncButton.Content = "Vypnúť ručnú synchronizáciu";
        }

        private void VisualiseButtons()
        {
            AutoSyncButton.Background = AUTO_SYNC_BUTTON_COLOR;
            ConfirmButton.Background = CONFIRM_BUTTON_COLOR;
            ManualSyncButton.Background = MANUAL_SYNC_BUTTON_COLOR;
            DeleteSyncButton.Background = DELETE_SYNC_BUTTON_COLOR;
            ChooseNewDatabasesButton.Background = CHOOSE_NEW_DATABASES_BUTTON_COLOR;
        }

        public void ReverseButtons()
        {
            List<UIElement> buttons = Buttons.Children.Cast<UIElement>().ToList();
            Buttons.Children.Clear();
            foreach (UIElement child in buttons.Reverse<UIElement>())
                Buttons.Children.Add(child);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e) => loadingWindow.Close();
    }
}
