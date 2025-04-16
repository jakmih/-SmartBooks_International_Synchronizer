using InternationalSynchronizer.UpdateVectorItemTable.Model;
using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using static InternationalSynchronizer.UpdateVectorItemTable.DB.SqlQuery;

namespace InternationalSynchronizer.UpdateVectorItemTable
{
    public class UpdateVectorItemTableService : IUpdateVectorItemTableService
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
            blob.LoadData(connectionString, GetBlobQuery());

            Compare compare = new();
            compare.LoadData(connection, GetVectorItemsQuery(databaseId));

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
    }
}
