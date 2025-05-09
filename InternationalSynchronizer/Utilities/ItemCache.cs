using System.Diagnostics;

namespace InternationalSynchronizer.Utilities
{
    public class ItemCache
    {
        private readonly Dictionary<Layer, Dictionary<Int32, List<string>>> _itemCache = [];
        private readonly DataManager _dataManager;

        public ItemCache(DataManager dataManager)
        {

            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
                _itemCache.Add(layer, []);

            _dataManager = dataManager;
        }

        public List<string> GetItem(Layer layer, Int32 id)
        {
            if (_itemCache[layer].TryGetValue(id, out List<string>? value))
                return value;

            List<string> item = _dataManager.LoadItem(layer, id);
            _itemCache[layer].Add(id, item);
            return item;
        }
    }
}