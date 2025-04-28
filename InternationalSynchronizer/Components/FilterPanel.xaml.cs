using System.Windows;
using System.Windows.Controls;
using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Components
{
    public partial class FilterPanel : UserControl
    {
        private Filter _filter = new();
        public Filter Filter => _filter;
        private Filter _filterStorage = new();
        public Filter FilterStorage => _filterStorage;
        private bool _loadingComboBoxes = false;

        public event Func<Task>? FiltereChanged;

        public FilterPanel()
        {
            InitializeComponent();
            _filterStorage.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
            BackButton.Background = BACK_BUTTON_COLOR;
        }

        public async Task LoadSubjectsAsync()
        {
            Layer previousLayer = _filter.Layer;
            _filter.Layer = Layer.Subject;
            await FiltereChanged!.Invoke();
            BackButton.IsEnabled = false;
        }

        private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem == null || _loadingComboBoxes)
                return;

            Layer previousLayer = _filter.Layer;

            if (comboBox == SubjectComboBox)
                _filter.Layer = Layer.Package;
            else if (comboBox == PackageComboBox)
                _filter.Layer = Layer.Theme;
            else if (comboBox == ThemeComboBox)
                _filter.Layer = Layer.Knowledge;
            else if (comboBox == KnowledgeComboBox)
                _filter.Layer = Layer.KnowledgeType;
            else
                return;
            
            _filter.SetLayerId(comboBox.SelectedIndex);

            try
            {
                await FiltereChanged!.Invoke();
                BackButton.IsEnabled = true;
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\n\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
                _filter.Layer = previousLayer;
                comboBox.SelectedIndex = -1;
            }
        }

        public void DataGridSelectionChanged(int index)
        {
            switch (_filter.Layer)
            {
                case Layer.Subject:
                    SubjectComboBox.SelectedIndex = index;
                    break;
                case Layer.Package:
                    PackageComboBox.SelectedIndex = index;
                    break;
                case Layer.Theme:
                    ThemeComboBox.SelectedIndex = index;
                    break;
                case Layer.Knowledge:
                    KnowledgeComboBox.SelectedIndex = index;
                    break;
                case Layer.KnowledgeType:
                    break;
            }
        }

        public void UpdateFilter(List<string> filterData)
        {
            EnableComboBox(_filter.Layer);
            ComboBox? currentComboBox = GetCombobox(_filter.Layer);

            if (currentComboBox != null)
                currentComboBox.ItemsSource = filterData;
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

        public void SwapFilters()
        {
            _filter.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
            (_filterStorage, _filter) = (_filter, _filterStorage);
            LoadComboBoxes();
            BackButton.IsEnabled = _filter.Layer != Layer.Subject;
        }

        private void LoadComboBoxes()
        {
            _loadingComboBoxes = true;
            List<ComboBox> comboBoxes = [SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox];

            int i = 0;
            foreach (var itemSource in _filter.GetItemSources())
                if (i != (int)_filter.Layer)
                    comboBoxes[i++].ItemsSource = itemSource;

            i = 0;
            foreach (var selectedItem in _filter.GetSelectedItems())
                if (i != (int)_filter.Layer)
                    comboBoxes[i++].SelectedItem = selectedItem;

            _loadingComboBoxes = false;
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filter.Layer == Layer.Subject)
                return;

            Layer previousLayer = _filter.Layer;

            _filter.Layer--;

            try
            {
                switch (_filter.Layer)
                {
                    case Layer.Subject:
                        await LoadSubjectsAsync();
                        break;
                    case Layer.Package:
                        await FiltereChanged!.Invoke();
                        break;
                    case Layer.Theme:
                        await FiltereChanged!.Invoke();
                        break;
                    case Layer.Knowledge:
                        await FiltereChanged!.Invoke();
                        break;
                }
            }
            catch (SqlException ex)
            {
                _filter.Layer = previousLayer;
                MessageBox.Show("Skontrolujte internetové pripojenie a skúste znova. Pokiaľ problem pretrváva, kontaktujte Vedenie.\n\nSpráva erroru: " + ex.Message,
                                "Chyba siete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public (Int32, Int32) GetIdsToSync(int leftRowIndex, int rightRowIndex) => new(_filterStorage.GetIdByRow(leftRowIndex), _filter.GetIdByRow(rightRowIndex));
    }
}
