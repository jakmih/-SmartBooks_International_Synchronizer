namespace InternationalSynchronizer.UpdateVectorItemTable.Model
{
    public class Package
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<Theme> Themes { get; set; } = [];
    }
}
