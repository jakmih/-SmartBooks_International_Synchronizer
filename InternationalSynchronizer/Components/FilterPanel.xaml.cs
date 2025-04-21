using System.Windows;
using System.Windows.Controls;
using InternationalSynchronizer.Utilities;
using static InternationalSynchronizer.Utilities.AppColors;

namespace InternationalSynchronizer.Components
{
    public partial class FilterPanel : UserControl
    {
        private Filter filter = new();
        private Filter filterStorage = new();
        private bool loadingComboBoxes = false;
        private Layer previousLayer = Layer.Subject;

        public event Action<Filter>? FiltereChanged;

        public FilterPanel()
        {
            InitializeComponent();
            filterStorage.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
            BackButton.Background = BACK_BUTTON_COLOR;
        }

        public void LoadSubjects()
        {
            BackButton.IsEnabled = false;
            previousLayer = filter.GetLayer();
            filter.SetLayer(Layer.Subject);
            FiltereChanged?.Invoke(filter);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem == null || loadingComboBoxes)
                return;

            BackButton.IsEnabled = false;
            previousLayer = filter.GetLayer();

            if (comboBox == SubjectComboBox)
                filter.SetLayer(Layer.Package);
            else if (comboBox == PackageComboBox)
                filter.SetLayer(Layer.Theme);
            else if (comboBox == ThemeComboBox)
                filter.SetLayer(Layer.Knowledge);
            else if (comboBox == KnowledgeComboBox)
                filter.SetLayer(Layer.KnowledgeType);
            else
                return;
            
            filter.SetLayerId(comboBox.SelectedIndex);

            FiltereChanged?.Invoke(filter);
            BackButton.IsEnabled = true;
        }

        public void DataGridSelectionChanged(int index)
        {
            BackButton.IsEnabled = false;
            switch (filter.GetLayer())
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
            EnableComboBox(filter.GetLayer());
            ComboBox? currentComboBox = GetCombobox(filter.GetLayer());

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
            filter.SaveComboBoxes([SubjectComboBox, PackageComboBox, ThemeComboBox, KnowledgeComboBox]);
            (filterStorage, filter) = (filter, filterStorage);
            LoadComboBoxes();

            previousLayer = filter.GetLayer();
            FiltereChanged?.Invoke(filter);
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (filter.GetLayer() == Layer.Subject)
                return;

            BackButton.IsEnabled = false;

            previousLayer = filter.GetLayer();

            filter.SetLayer(filter.GetLayer() - 1);

            switch (filter.GetLayer())
            {
                case Layer.Subject:
                    LoadSubjects();
                    break;
                case Layer.Package:
                    FiltereChanged?.Invoke(filter);
                    BackButton.IsEnabled = true;
                    break;
                case Layer.Theme:
                    FiltereChanged?.Invoke(filter);
                    BackButton.IsEnabled = true;
                    break;
                case Layer.Knowledge:
                    FiltereChanged?.Invoke(filter);
                    BackButton.IsEnabled = true;
                    break;
            }
        }

        public Layer GetLayer() => filter.GetLayer();

        public Filter GetFilter() => filter;

        public (Int32, Int32) GetIdsToSync(int leftRowIndex, int rightRowIndex) => new(filterStorage.GetIdByRow(leftRowIndex), filter.GetIdByRow(rightRowIndex));

        public void SetPreviousLayer() => filter.SetLayer(previousLayer);
    }
}
