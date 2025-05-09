namespace InternationalSynchronizer.Utilities
{
    public record FullData(MyGridMetadata LeftMetadata, MyGridMetadata RightMetadata, List<string> FilterData, Layer Layer)
    {
        public MyGridMetadata LeftMetadata = LeftMetadata;
        public MyGridMetadata RightMetadata = RightMetadata;
        public List<string> FilterData = FilterData;
        public Layer Layer = Layer;
    }
}
