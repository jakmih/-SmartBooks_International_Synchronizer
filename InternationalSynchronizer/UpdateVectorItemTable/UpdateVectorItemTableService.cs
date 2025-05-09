﻿using InternationalSynchronizer.UpdateVectorItemTable.Model;
using InternationalSynchronizer.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http;
using static InternationalSynchronizer.UpdateVectorItemTable.DB.SqlQuery;

namespace InternationalSynchronizer.UpdateVectorItemTable
{
    public class UpdateVectorItemTableService : IUpdateVectorItemTableService
    {
        public bool RunUpdate()
        {
            IConfiguration config = AppSettingsLoader.LoadConfiguration();
            using var connection = new SqlConnection(config.GetConnectionString("Sync"));
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                //using var truncateCommand = new SqlCommand("TRUNCATE TABLE vector_item", connection, transaction);
                //truncateCommand.CommandTimeout = 600;
                //truncateCommand.ExecuteNonQuery();

                int updated = 0;
                updated += UpdateDatabase(connection, transaction);

                transaction.Commit();

                //if (updated > 0)
                //    return SendHTTPRequestForVectorization();

                return false;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Debug.WriteLine("Transaction failed: " + ex.Message);
                return false;
            }
        }

        private static int UpdateDatabase(SqlConnection connection, SqlTransaction transaction)
        {
            List<Subject> subjects = LoadData(connection, transaction);
            DataTable stagingTable = CreateStagingTable(connection, transaction, subjects);

            using (var command = new SqlCommand(DeleteRemovedRows(), connection, transaction))
            {
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }

            using (var command = new SqlCommand(UpdateChangedRows(), connection, transaction))
            {
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }

            using (var command = new SqlCommand(InsertNewRows(), connection, transaction))
            {
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }

            return stagingTable.Rows.Count;
        }

        private static DataTable CreateStagingTable(SqlConnection connection, SqlTransaction transaction, List<Subject> subjects)
        {
            var stagingTable = new DataTable();
            stagingTable.Columns.Add("id_database", typeof(int));
            stagingTable.Columns.Add("id_item", typeof(int));
            stagingTable.Columns.Add("id_item_type", typeof(int));
            stagingTable.Columns.Add("name", typeof(string));
            stagingTable.Columns.Add("id_subject", typeof(int));
            stagingTable.Columns.Add("id_package", typeof(int));
            stagingTable.Columns.Add("id_theme", typeof(int));
            stagingTable.Columns.Add("id_knowledge_type", typeof(int));
            stagingTable.Columns.Add("date_modified", typeof(DateTime));

            FillStagingTable(subjects, stagingTable);

            using (var command = new SqlCommand(CreateTemporaryTable(), connection, transaction))
            {
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#vector_item_staging";
                bulkCopy.ColumnMappings.Add("id_database", "id_database");
                bulkCopy.ColumnMappings.Add("id_item", "id_item");
                bulkCopy.ColumnMappings.Add("id_item_type", "id_item_type");
                bulkCopy.ColumnMappings.Add("name", "name");
                bulkCopy.ColumnMappings.Add("id_subject", "id_subject");
                bulkCopy.ColumnMappings.Add("id_package", "id_package");
                bulkCopy.ColumnMappings.Add("id_theme", "id_theme");
                bulkCopy.ColumnMappings.Add("id_knowledge_type", "id_knowledge_type");
                bulkCopy.ColumnMappings.Add("date_modified", "date_modified");
                bulkCopy.WriteToServer(stagingTable);
            }

            return stagingTable;
        }

        private static void FillStagingTable(List<Subject> subjects, DataTable stagingTable)
        {
            DateTime now = DateTime.UtcNow;

            foreach (var subject in subjects)
            {
                foreach (var package in subject.Packages)
                {
                    if (!string.IsNullOrEmpty(package.Name))
                        stagingTable.Rows.Add(subject.DatabaseId,
                                              package.Id,
                                              1,
                                              package.Name,
                                              subject.Id,
                                              DBNull.Value,
                                              DBNull.Value,
                                              DBNull.Value,
                                              now);

                    foreach (var theme in package.Themes)
                    {
                        if (!string.IsNullOrEmpty(theme.Name))
                            stagingTable.Rows.Add(subject.DatabaseId,
                                                  theme.Id,
                                                  2,
                                                  theme.Name,
                                                  subject.Id,
                                                  package.Id,
                                                  DBNull.Value,
                                                  DBNull.Value,
                                                  now);

                        foreach (var knowledge in theme.Knowledges)
                        {
                            stagingTable.Rows.Add(subject.DatabaseId,
                                                  knowledge.Id,
                                                  3,
                                                  knowledge.Name,
                                                  subject.Id,
                                                  package.Id,
                                                  theme.Id,
                                                  knowledge.Type == null ? DBNull.Value : knowledge.Type,
                                                  now);
                        }
                    }
                }
            }
        }

        private static List<Subject> LoadData(SqlConnection connection, SqlTransaction transaction)
        {
            using var command = new SqlCommand(AllKnowledgesQuery(), connection, transaction);
            command.CommandTimeout = 600;
            using var reader = command.ExecuteReader();

            List<Subject> subjects = [];
            Dictionary<(int, int), Subject> subjectDict = [];
            Dictionary<(int, int), Package> packageDict = [];
            Dictionary<(int, int), Theme> themeDict = [];

            while (reader.Read())
            {
                int databaseId = reader.IsDBNull(9) ? 0 : Int32.Parse(reader.GetString(9));
                int subjectId = reader.GetInt32(0);
                if (!subjectDict.TryGetValue((databaseId, subjectId), out var subject))
                {
                    subject = new Subject { Id = subjectId, Name = reader.IsDBNull(1) ? "" : reader.GetString(1), DatabaseId = databaseId };
                    subjectDict[(databaseId, subjectId)] = subject;
                    subjects.Add(subject);
                }

                if (!reader.IsDBNull(2))
                {
                    int packageId = reader.GetInt32(2);
                    if (!packageDict.TryGetValue((databaseId, packageId), out var package))
                    {
                        package = new Package { Id = packageId, Name = reader.IsDBNull(3) ? "" : reader.GetString(3) };
                        packageDict[(databaseId, packageId)] = package;
                        subject.Packages.Add(package);
                    }

                    if (!reader.IsDBNull(4))
                    {
                        int themeId = reader.GetInt32(4);
                        if (!themeDict.TryGetValue((databaseId, themeId), out var theme))
                        {
                            theme = new Theme { Id = themeId, Name = reader.IsDBNull(5) ? "" : reader.GetString(5) };
                            themeDict[(databaseId, themeId)] = theme;
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
            return subjects;
        }

        private static bool SendHTTPRequestForVectorization()
        {
            using var httpClient = new HttpClient();

            var endpoint = "https://testaistorage.search.windows.net/indexers/sb-final-indexer/run?api-version=2020-06-30";
            var apiKey = App.Config["Keys:AISearchKey"];

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = httpClient.PostAsync(endpoint, null).Result;

            return response.IsSuccessStatusCode;
        }
    }
}
