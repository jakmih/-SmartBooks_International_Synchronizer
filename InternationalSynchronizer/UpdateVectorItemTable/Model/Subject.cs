namespace InternationalSynchronizer.UpdateVectorItemTable.Model
{
    public class Subject
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<Package> Packages { get; set; } = [];
        public int DatabaseId { get; set; }
    }
}
