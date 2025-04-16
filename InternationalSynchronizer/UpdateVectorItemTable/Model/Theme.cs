namespace InternationalSynchronizer.UpdateVectorItemTable.Model
{
    public class Theme
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<Knowledge> Knowledges { get; set; } = [];
    }
}
