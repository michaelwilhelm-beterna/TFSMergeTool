using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MergeHelper
{
    public static class ConfigStore
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MergeHelper");

        public static string FilePath => Path.Combine(Folder, "config.json");

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(FilePath), Settings) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Konfiguration konnte nicht gelesen werden:\n" + ex.Message,
                    "MergeHelper", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(config, Settings));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
