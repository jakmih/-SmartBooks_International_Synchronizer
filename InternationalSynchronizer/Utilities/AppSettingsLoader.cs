using Microsoft.Extensions.Configuration;
using System.IO;

namespace InternationalSynchronizer.Utilities
{
    public class AppSettingsLoader
    {
        public static IConfiguration LoadConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
        }
    }
}
