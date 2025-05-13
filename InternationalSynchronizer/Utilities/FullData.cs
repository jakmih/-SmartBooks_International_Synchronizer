namespace InternationalSynchronizer.Utilities
{
    public record FullData(MyGridMetadata MainMetadata, MyGridMetadata SyncMetadata, List<string> FilterData, Layer Layer)
    {
        public MyGridMetadata MainMetadata = MainMetadata;
        public MyGridMetadata SyncMetadata = SyncMetadata;
        public List<string> FilterData = FilterData;
        public Layer Layer = Layer;
    }
}
