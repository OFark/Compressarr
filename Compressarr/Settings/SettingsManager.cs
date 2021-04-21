using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public class SettingsManager : ISettingsManager
    {
        private const string settingsFile = "settings.json";
        private readonly ILogger<SettingsManager> logger;
        public SettingsManager(ILogger<SettingsManager> logger)
        {
            this.logger = logger;
        }

        public static string CodecOptionsDirectory => Path.Combine(ConfigDirectory, "CodecOptions");
        public static string ConfigDirectory => InDocker ? "/config" : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        public static string dockerAppSettings => Path.Combine(ConfigDirectory, "appsettings.json");
        public static string Group => Environment.GetEnvironmentVariable("PUID");
        public static bool InDocker => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

        public static string User => Environment.GetEnvironmentVariable("PUID");
        public Dictionary<string, string> Settings => _settings ?? LoadSettings();
        private Dictionary<string, string> _settings { get; set; }
        public void AddSetting(SettingType setting, string value)
        {
            using (logger.BeginScope("Add Setting"))
            {
                logger.LogDebug($"Setting Type: {setting}");

                var name = setting.ToString();

                if (Settings.ContainsKey(name))
                {
                    Settings[name] = value;
                }
                else
                {
                    Settings.Add(name, value);
                }

                SaveSettings();
            }
        }

        public void DeleteSetting(SettingType setting)
        {
            using (logger.BeginScope("Delete Setting"))
            {
                logger.LogDebug($"Setting Type: {setting}");
                var name = setting.ToString();

                if (Settings.ContainsKey(name))
                {
                    Settings.Remove(name);
                }

                SaveSettings();
            }
        }

        public string GetSetting(SettingType setting)
        {
            using (logger.BeginScope("Get Setting"))
            {
                logger.LogDebug($"Setting Type: {setting}");
                var name = setting.ToString();

                if (Settings.ContainsKey(name))
                {
                    return Settings[name];
                }

                return null;
            }
        }

        public bool HasSetting(SettingType setting) => Settings.ContainsKey(setting.ToString());

        public async Task<T> LoadSettingFile<T>(string fileName)
        {
            using (logger.BeginScope("Loading a Setting file"))
            {
                logger.LogInformation($"File name: {fileName}");

                var filePath = ConfigFile(fileName);

                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        return JsonConvert.DeserializeObject<T>(json);
                    }
                    logger.LogWarning("Settings file is empty");
                }
                else
                {
                    logger.LogInformation("Settings file does not exist");
                }

                return default;
            }
        }

        public async Task SaveSettingFile(string fileName, object content)
        {
            using (logger.BeginScope("Saving a Setting file"))
            {
                logger.LogInformation($"File name: {fileName}");

                var filePath = ConfigFile(fileName);

                var fileDir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(fileDir))
                {
                    logger.LogDebug($"Creating Directory: {fileDir}");
                    Directory.CreateDirectory(fileDir);


                }

                var json = JsonConvert.SerializeObject(content, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

                await File.WriteAllTextAsync(filePath, json);
            }
        }

        private string ConfigFile(string fileName) => Path.Combine(ConfigDirectory, fileName);
        private Dictionary<string, string> LoadSettings()
        {
            using (logger.BeginScope("Load Settings"))
            {
                _settings = new Dictionary<string, string>();

                _settings = LoadSettingFile<Dictionary<string, string>>(settingsFile).Result ?? new();

                return _settings;
            }
        }

        private void SaveSettings()
        {
            using (logger.BeginScope("Save Settings"))
            {
                _ = SaveSettingFile(settingsFile, Settings);
            }
        }
    }
}