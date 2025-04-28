using System.Windows.Controls;

namespace InternationalSynchronizer.Utilities
{
    public class Filter
    {
        private Layer _layer = Layer.Subject;
        public Layer Layer
        {
            get => _layer;
            set => _layer = value;
        }
        private readonly Dictionary<Layer, List<Int32>> _layerIds = [];
        private readonly Dictionary<Layer, Int32> _selectedUpperLayerId = [];
        private List<System.Collections.IEnumerable> _itemSources = [];
        private List<object> _selectedItems = [];

        public Filter()
        {
            _selectedUpperLayerId.Add(Layer.Subject, -1);
            _selectedUpperLayerId.Add(Layer.Package, -1);
            _selectedUpperLayerId.Add(Layer.Theme, -1);
            _selectedUpperLayerId.Add(Layer.Knowledge, -1);
            _selectedUpperLayerId.Add(Layer.KnowledgeType, -1);
            _layerIds.Add(Layer.Subject, []);
            _layerIds.Add(Layer.Package, []);
            _layerIds.Add(Layer.Theme, []);
            _layerIds.Add(Layer.Knowledge, []);
            _layerIds.Add(Layer.KnowledgeType, []);
        }

        public void SetIds(List<Int32> ids) => _layerIds[_layer] = [..ids];

        public Int32 GetUpperLayerId() => _selectedUpperLayerId[_layer];

        public Int32 GetIdByRow(Int32 rowIndex)
        {
            if (_layer == Layer.KnowledgeType)
                return _selectedUpperLayerId[_layer];

            return (_layerIds[_layer].Count > rowIndex && rowIndex >= 0) ? _layerIds[_layer][rowIndex] : -1;
        }

        public List<Int32> GetIds()
        {
            if (_layer == Layer.KnowledgeType)
                return [_selectedUpperLayerId[_layer]];

            return _layerIds[_layer];
        }

        public void SetLayerId(Int32 id_index)
        {
            if (_layer == Layer.Subject || id_index < 0 || id_index >= _layerIds[_layer - 1].Count)
            {
                _selectedUpperLayerId[_layer] = -1;
                return;
            }

            _selectedUpperLayerId[_layer] = _layerIds[_layer - 1][id_index];
        }

        public void SaveComboBoxes(List<ComboBox> comboBoxes)
        {
            _itemSources = [];
            _selectedItems = [];
            foreach (ComboBox comboBox in comboBoxes)
            {
                _itemSources.Add(comboBox.ItemsSource);
                _selectedItems.Add(comboBox.SelectedItem);
            }
        }

        public int GetSubjectId() => _selectedUpperLayerId[Layer.Package];

        public List<System.Collections.IEnumerable> GetItemSources() => _itemSources;

        public List<object> GetSelectedItems() => _selectedItems;
    }
}
