using Microsoft.Data.SqlClient;

namespace InternationalSynchronizer.UpdateVectorItemTable
{
    partial class UpdateVectorItemTableService
    {
        private class Compare
        {
            public List<int> PackageIds { get; set; } = [];
            public List<int> ThemeIds { get; set; } = [];
            public List<int> KnowledgeIds { get; set; } = [];
            public void LoadData(SqlConnection connection, string query)
            {
                var command = new SqlCommand(query, connection);
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    switch (reader.GetInt32(0))
                    {
                        case 1:
                            PackageIds.Add(reader.GetInt32(1));
                            break;
                        case 2:
                            ThemeIds.Add(reader.GetInt32(1));
                            break;
                        case 3:
                            KnowledgeIds.Add(reader.GetInt32(1));
                            break;
                    }
                }
                reader.Close();
            }
        }
    }
}
