using Microsoft.Data.SqlClient;

namespace InternationalSynchronizer.UpdateVectorItemTable
{
    partial class UpdateVectorItemTableService
    {
        private class Blob
        {
            public List<Subject> Subjects { get; set; } = [];

            public void LoadData(string connectionString, string query)
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                using var command = new SqlCommand(query, connection);
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
    }
}
