using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Controls;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Components
{
    public partial class ActionButtons : UserControl
    {
        private readonly LoadingWindow _loadingWindow = new();
        public LoadingWindow LoadingWindow => _loadingWindow;

        public ActionButtons()
        {
            InitializeComponent();
            VisualiseButtons();
        }

        public event Func<Task<int>>? AutoSync;
        public event Func<Task<int>>? ConfirmSync;
        public event Func<Task>? ManualSync;
        public event Func<Task>? DeleteSync;
        public event Action? NewDatabases;

        private async void AutoSyncButton_Click(object sender, RoutedEventArgs e)
        {
            _loadingWindow.UpdateText("Hľadajú sa nové synchronizácie, prosím čakajte...");
            _loadingWindow.Show();

            await SafeRunAsync(AutoSyncButtonAsync);
        }

        private async Task AutoSyncButtonAsync()
        {
            int status = await AutoSync!.Invoke();
            _loadingWindow.Hide();
            switch (status)
            {
                case -2:
                    MessageBox.Show("AI synchronizácia sa nedá použiť na predmety.\nPoužite manuálnu synchronizáciu.",
                                    "Nemožná AI synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case -1:
                    MessageBox.Show("Na synchronizovanie položiek v danom predmete je potrebné najprv synchronizovať daný predmet.",
                                    "Synchronizujte predmet", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case 0:
                    MessageBox.Show("Nenašla sa žiadna nová synchronizácia.",
                                    "Žiadna synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    break;
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            _loadingWindow.UpdateText("Ukladajú sa zmeny, prosím čakajte...");
            _loadingWindow.Show();

            await SafeRunAsync(ConfirmButtonAsync);
        }

        private async Task ConfirmButtonAsync()
        {
            int status = await ConfirmSync!.Invoke();
            _loadingWindow.Hide();
            switch (status)
            {
                case -4:
                    MessageBox.Show("Na potvrdenie synchronizácie úlohy musíš označiť 1 položku v pravej tabuľke.",
                                    "Nevybraná synchronizácia", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case -3:
                    MessageBox.Show("Vybraná položka už je synchronizovaná.\nVyberte položku, ktorá nie je synchronizovaná, alebo jej synchronizáciu zrušte.",
                                    "Položka už je synchronizovaná", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case -2:
                    MessageBox.Show("Na synchronizovanie položiek musíš označiť 1 položku v ľavej tabuľke a 1 položku v pravej tabuľke.",
                                    "Nevybrany pár", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case -1:
                    MessageBox.Show("Synchronizovať môžeš iba položky na rovnakej úrovni:\nPredmet-Predmet\nBalíček-Balíček\n...",
                                    "Nesprávna úroveň", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    break;
            }
        }

        private async void ManualSyncButton_Click(object sender, RoutedEventArgs e)
        {
            await SafeRunAsync(ManualSync!.Invoke);
        }

        private async void DeleteSyncButton_Click(object sender, RoutedEventArgs e)
        {
            await SafeRunAsync(DeleteSync!.Invoke);
        }

        private void ChooseNewDatabasesButton_Click(object sender, RoutedEventArgs e)
        {
            NewDatabases!.Invoke();
        }

        private async Task SafeRunAsync(Func<Task> action)
        {
            try
            {
                await action();
                _loadingWindow.Hide();
            }
            catch (SqlException exception)
            {
                _loadingWindow.Hide();
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\n\nSpráva erroru: " + exception.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            List<UIElement> buttons = [.. Buttons.Children.Cast<UIElement>()];
            Buttons.Children.Clear();
            foreach (UIElement child in buttons.Reverse<UIElement>())
                Buttons.Children.Add(child);
        }
    }
}
