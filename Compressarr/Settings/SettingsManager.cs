using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Compressarr.Settings
{
    public class SettingsManager : ISettingsManager
    {
        private string settingsFilePath => Path.Combine(_env.ContentRootPath, "config", "settings.json");
        private Dictionary<string, string> _settings { get; set; }

        public Dictionary<string, string> Settings => _settings ?? LoadSettings();

        private IWebHostEnvironment _env;

        public SettingsManager(IWebHostEnvironment env)
        {
            _env = env;
        }

        public void AddSetting(SettingType setting, string value)
        {
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

        public void DeleteSetting(SettingType setting)
        {
            var name = setting.ToString();

            if (Settings.ContainsKey(name))
            {
                Settings.Remove(name);
            }

            SaveSettings();
        }

        public string GetSetting(SettingType setting)
        {
            var name = setting.ToString();

            if (Settings.ContainsKey(name))
            {
                return Settings[name];
            }

            return null;
        }

        public bool HasSetting(SettingType setting) => Settings.ContainsKey(setting.ToString());

        private void SaveSettings()
        {
            var json = JsonConvert.SerializeObject(Settings, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(settingsFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath));
            }

            File.WriteAllText(settingsFilePath, json);
        }

        private Dictionary<string, string> LoadSettings()
        {
            _settings = new Dictionary<string, string>();

            if (File.Exists(settingsFilePath))
            {
                var json = File.ReadAllText(settingsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }

            return _settings;
        }
    }
}