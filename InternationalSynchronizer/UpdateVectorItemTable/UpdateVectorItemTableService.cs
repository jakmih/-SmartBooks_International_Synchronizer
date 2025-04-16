using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace InternationalSynchronizer.UpdateVectorItemTable
{
    class UpdateVectorItemTableService : IUpdateVectorItemTableService
    {
        public int RunUpdate()
        {
            IConfiguration config = AppSettingsLoader.LoadConfiguration();

            Dictionary<string, string> databases = [];
            foreach (string database in config.GetRequiredSection("connectionStrings").GetChildren().Select(x => x.Key).Where(x => x != "Sync"))
                databases.Add(database, config.GetConnectionString(database)!);

            using var connection = new SqlConnection(config.GetConnectionString("Sync"));
            connection.Open();

            int updated = 0;
            foreach (string database in databases.Keys)
                updated += UpdateOneDatabase(databases[database], GetDatabaseId(database, connection), connection);

            return updated;
        }

        private static int GetDatabaseId(string database, SqlConnection connection)
        {
            using var command = new SqlCommand(GetDatabaseIdQuery(database), connection);
            using var reader = command.ExecuteReader();

            if (reader.Read())
                return reader.GetInt32(0);

            reader.Close();
            return CreateDatabase(database, connection);
        }

        private static int CreateDatabase(string database, SqlConnection connection)
        {
            using var command = new SqlCommand(GetInsertDatabaseQuery(database), connection);
            command.ExecuteNonQuery();

            using var idCommand = new SqlCommand("SELECT SCOPE_IDENTITY()", connection);
            return Convert.ToInt32(idCommand.ExecuteScalar());
        }

        private static int UpdateOneDatabase(string connectionString, int databaseId, SqlConnection connection)
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
                                                     knowledge.Type == null ? DBNull.Value : knowledge.Type);
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

        private static string GetDatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string GetInsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        private static string GetVectorItemsQuery(int databaseId)
        {
            return $"SELECT id_item_type, id_item FROM vector_item WHERE id_database = {databaseId}";
        }

        private static string GetBlobQuery()
        {
            return @"
            SELECT 
                sub.id, sub.name,
                pac.id, pac.name + ' - ' + pac.description,
                thm.id, thm.name,
                tsk.id, tsk.knowledge_text_preview, tsk_t.id
            FROM subject_type AS sub
            LEFT JOIN package AS pac ON sub.id = pac.id_subject_type
            LEFT JOIN theme AS thm ON pac.id = thm.id_package
            LEFT JOIN theme_part AS thm_p ON thm_p.id_theme = thm.id
            LEFT JOIN knowledge AS tsk ON tsk.id_theme_part = thm_p.id
            LEFT JOIN knowledge_type AS tsk_t ON tsk_t.id = tsk.id_knowledge_type
            ORDER BY sub.id, pac.id, thm.id, tsk.id";
        }
    }
}
