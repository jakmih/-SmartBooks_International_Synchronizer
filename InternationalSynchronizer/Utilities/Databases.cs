using Microsoft.Data.SqlClient;
using System.Data;
using static InternationalSynchronizer.Utilities.SqlQuery;

namespace InternationalSynchronizer.Utilities
{
    class Databases(string synchronizationConnectionString, Dictionary<string, string> databases)
    {
        private readonly string synchronizationConnectionString = synchronizationConnectionString;
        private readonly Dictionary<string, string> databases = databases;

        public static Int32 GetDatabaseId(string database, SqlConnection connection)
        {
            using var command = new SqlCommand(GetDatabaseIdQuery(database), connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
                return reader.GetInt32(0);

            reader.Close();
            return CreateDatabase(database, connection);
        }

        private static Int32 CreateDatabase(string database, SqlConnection connection)
        {
            using var command = new SqlCommand(GetInsertDatabaseQuery(database), connection);
            command.ExecuteNonQuery();

            using var idCommand = new SqlCommand("SELECT SCOPE_IDENTITY()", connection);
            return Convert.ToInt32(idCommand.ExecuteScalar());
        }

        public int Update()
        {
            using var connection = new SqlConnection(synchronizationConnectionString);
            connection.Open();
            
            int updated = 0;
            foreach (string database in databases.Keys)
                updated += OneDatabase(databases[database], GetDatabaseId(database, connection), connection);
            
            return updated;
        }

        private static int OneDatabase(string connectionString, Int32 databaseId, SqlConnection connection)
        {
            Blob blob = new();
            blob.LoadData(connectionString);

            Compare compare = new();
            compare.LoadData(connection, databaseId);

            var vectorTable = new DataTable();
            vectorTable.Columns.Add("id_database", typeof(int));
            vectorTable.Columns.Add("id_subject", typeof(int));
            vectorTable.Columns.Add("id_item", typeof(int));
            vectorTable.Columns.Add("id_item_type", typeof(int));
            vectorTable.Columns.Add("name", typeof(string));
            vectorTable.Columns.Add("id_knowledge_type", typeof(int));

            foreach (var subject in blob.Subjects)
            {
                foreach (var package in subject.Packages)
                {
                    if (package.Name.StartsWith("*IMPORT*"))
                        continue;

                    if (!compare.PackageIds.Contains(package.Id))
                        vectorTable.Rows.Add(databaseId,
                                             subject.Id,
                                             package.Id,
                                             1,
                                             string.IsNullOrEmpty(package.Name) ? DBNull.Value : package.Name,
                                             DBNull.Value);

                    foreach (var theme in package.Themes)
                    {
                        if (theme.Name.StartsWith("*IMPORT*"))
                            continue;

                        if (!compare.ThemeIds.Contains(theme.Id))
                            vectorTable.Rows.Add(databaseId,
                                                 subject.Id,
                                                 theme.Id,
                                                 2,
                                                 string.IsNullOrEmpty(theme.Name) ? DBNull.Value : theme.Name,
                                                 DBNull.Value);

                        foreach (var knowledge in theme.Knowledges)
                        {
                            if (knowledge.Name.StartsWith("*IMPORT*"))
                                continue;

                            if (!compare.KnowledgeIds.Contains(knowledge.Id))
                                vectorTable.Rows.Add(databaseId,
                                                     subject.Id,
                                                     knowledge.Id,
                                                     3,
                                                     string.IsNullOrEmpty(knowledge.Name) ? DBNull.Value : knowledge.Name,
                                                     (knowledge.Type == null) ? DBNull.Value : knowledge.Type);
                        }
                    }
                }
            }

            using var bulkCopy = new SqlBulkCopy(connection){ DestinationTableName = "vector_item" };
            bulkCopy.ColumnMappings.Add("id_database", "id_database");
            bulkCopy.ColumnMappings.Add("id_subject", "id_subject");
            bulkCopy.ColumnMappings.Add("id_item", "id_item");
            bulkCopy.ColumnMappings.Add("id_item_type", "id_item_type");
            bulkCopy.ColumnMappings.Add("name", "name");
            bulkCopy.ColumnMappings.Add("id_knowledge_type", "id_knowledge_type");
            bulkCopy.WriteToServer(vectorTable);

            return vectorTable.Rows.Count;
        }


        private class Compare
        {
            public List<int> PackageIds { get; set; } = [];
            public List<int> ThemeIds { get; set; } = [];
            public List<int> KnowledgeIds { get; set; } = [];
            public void LoadData(SqlConnection connection, int databaseId)
            {
                var command = new SqlCommand(GetVectorItemsQuery(databaseId), connection);
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

        private class Blob
        {
            public List<Subject> Subjects { get; set; } = [];

            public void LoadData(string connectionString)
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                using var command = new SqlCommand(GetBlobQuery(), connection);
                using var reader = command.ExecuteReader();

                var subjectDict = new Dictionary<int, Subject>();
                var packageDict = new Dictionary<int, Package>();
                var themeDict = new Dictionary<int, Theme>();

                while (reader.Read())
                {

                    int subjectId = reader.GetInt32(0);
                    if (!subjectDict.TryGetValue(subjectId, out var subject))
                    {
                        subject = new Subject { Id = subjectId, Name = reader.IsDBNull(1) ? "" : reader.GetString(1) };
                        subjectDict[subjectId] = subject;
                        Subjects.Add(subject);
                    }

                    if (!reader.IsDBNull(2))
                    {
                        int packageId = reader.GetInt32(2);
                        if (!packageDict.TryGetValue(packageId, out var package))
                        {
                            package = new Package { Id = packageId, Name = reader.IsDBNull(3) ? "" : reader.GetString(3) };
                            packageDict[packageId] = package;
                            subject.Packages.Add(package);
                        }

                        if (!reader.IsDBNull(4))
                        {
                            int themeId = reader.GetInt32(4);
                            if (!themeDict.TryGetValue(themeId, out var theme))
                            {
                                theme = new Theme { Id = themeId, Name = reader.IsDBNull(5) ? "" : reader.GetString(5) };
                                themeDict[themeId] = theme;
                                package.Themes.Add(theme);
                            }

                            if (!reader.IsDBNull(6))
                            {
                                Knowledge knowledge = new()
                                {
                                    Id = reader.GetInt32(6),
                                    Name = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    Type = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                                };
                                theme.Knowledges.Add(knowledge);
                            }
                        }
                    }
                }
            }
        }

        private class Subject
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public List<Package> Packages { get; set; } = [];
        }

        private class Package
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public List<Theme> Themes { get; set; } = [];
        }

        private class Theme
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public List<Knowledge> Knowledges { get; set; } = [];
        }

        private class Knowledge
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int? Type { get; set; }
        }
    }
}
