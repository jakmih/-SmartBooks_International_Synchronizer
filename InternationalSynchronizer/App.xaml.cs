using InternationalSynchronizer.Utilities;
using Microsoft.Extensions.Configuration;
using System.Windows;

namespace InternationalSynchronizer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly IConfiguration _config = AppSettingsLoader.LoadConfiguration();
        public static IConfiguration Config => _config;
    }
}
