namespace InternationalSynchronizer.UpdateVectorItemTable
{
    partial class UpdateVectorItemTableService
    {
        private class Subject
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public List<Package> Packages { get; set; } = [];
        }
    }
}
