using System.Windows.Controls;

namespace InternationalSynchronizer.Utilities
{
    public class Filter
    {
        private Layer layer = Layer.Subject;
        private readonly Dictionary<Layer, List<Int32>> layerIds = [];
        private readonly Dictionary<Layer, Int32> selectedUpperLayerId = [];
        private List<System.Collections.IEnumerable> itemSources = [];
        private List<object> selectedItems = [];

        public Filter()
        {
            selectedUpperLayerId.Add(Layer.Subject, -1);
            selectedUpperLayerId.Add(Layer.Package, -1);
            selectedUpperLayerId.Add(Layer.Theme, -1);
            selectedUpperLayerId.Add(Layer.Knowledge, -1);
            selectedUpperLayerId.Add(Layer.KnowledgeType, -1);
            layerIds.Add(Layer.Subject, []);
            layerIds.Add(Layer.Package, []);
            layerIds.Add(Layer.Theme, []);
            layerIds.Add(Layer.Knowledge, []);
            layerIds.Add(Layer.KnowledgeType, []);
        }

        public Layer GetLayer() => layer;

        public void SetLayer(Layer layer) => this.layer = layer;

        public void SetIds(List<Int32> ids) => layerIds[layer] = [..ids];

        public Int32 GetUpperLayerId() => selectedUpperLayerId[layer];

        public Int32 GetIdByRow(Int32 rowIndex)
        {
            if (layer == Layer.KnowledgeType)
                return selectedUpperLayerId[layer];

            return (layerIds[layer].Count > rowIndex && rowIndex >= 0) ? layerIds[layer][rowIndex] : -1;
        }

        public List<Int32> GetIds()
        {
            if (layer == Layer.KnowledgeType)
                return [selectedUpperLayerId[layer]];

            return layerIds[layer];
        }

        public void SetLayerId(Int32 id_index)
        {
            if (layer == Layer.Subject || id_index < 0 || id_index >= layerIds[layer - 1].Count)
            {
                selectedUpperLayerId[layer] = -1;
                return;
            }

            selectedUpperLayerId[layer] = layerIds[layer - 1][id_index];
        }

        public void SaveComboBoxes(List<ComboBox> comboBoxes)
        {
            itemSources = [];
            selectedItems = [];
            foreach (ComboBox comboBox in comboBoxes)
            {
                itemSources.Add(comboBox.ItemsSource);
                selectedItems.Add(comboBox.SelectedItem);
            }
        }

        public int GetSubjectId() => selectedUpperLayerId[Layer.Package];

        public int GetKnowledgeId() => selectedUpperLayerId[Layer.KnowledgeType];

        public List<System.Collections.IEnumerable> GetItemSources() => itemSources;

        public List<object> GetSelectedItems() => selectedItems;
    }
}
