using Microsoft.Data.SqlClient;
using System.Diagnostics;
using static InternationalSynchronizer.Utilities.SqlQuery;

namespace InternationalSynchronizer.Utilities
{
    public class ItemCache
    {
        private readonly Dictionary<Layer, Dictionary<Int32, List<string>>> _itemCache = [];
        private readonly Dictionary<Layer, Dictionary<Int32, (int synced, int total)>> _syncedChildCountCache = [];
        private readonly DataManager _dataManager;

        public ItemCache(DataManager dataManager)
        {

            foreach (Layer layer in Enum.GetValues(typeof(Layer)))
            {
                _itemCache.Add(layer, []);
                _syncedChildCountCache.Add(layer, []);
            }
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

        public void AddChildCount(Layer layer, Int32 id, (int synced, int total) newChildCount)
        {
            if (layer != Layer.Subject && layer != Layer.Package && layer != Layer.Theme)
                return;

            if (_syncedChildCountCache[layer].TryGetValue(id, out var childCount))
            {
                if (childCount.synced == -1)
                    childCount.synced = newChildCount.synced;

                if (childCount.total == -1)
                    childCount.total = newChildCount.total;

                _syncedChildCountCache[layer][id] = childCount;
            }
            else
                _syncedChildCountCache[layer].Add(id, newChildCount);
        }

        public void AddToSyncedChildCount(Layer layer, Int32 id, int syncedChildCount)
        {
            Debug.WriteLine($"AddToSyncedChildCount: {layer} {id} {syncedChildCount}");
            if (layer != Layer.Subject && layer != Layer.Package && layer != Layer.Theme)
                return;
            Debug.WriteLine("DONE");

            if (_syncedChildCountCache[layer].TryGetValue(id, out var childCount))
            {
                childCount.synced += syncedChildCount;
                _syncedChildCountCache[layer][id] = childCount;
            }
            else
                AddChildCount(layer, id, (syncedChildCount, -1));
        }

        public string GetSyncedChildCount(Layer layer, Int32 id)
        {
            if ((layer != Layer.Subject && layer != Layer.Package && layer != Layer.Theme)
                || !_syncedChildCountCache[layer].TryGetValue(id, out var childCount))
                return "";

            string syncedText = childCount.synced == -1 ? "?" : childCount.synced.ToString();
            string totalText = childCount.total == -1 ? "?" : childCount.total.ToString();
            return $"{syncedText}/{totalText}";
        }
    }
}