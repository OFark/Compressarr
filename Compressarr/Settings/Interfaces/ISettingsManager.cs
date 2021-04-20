using System.Collections.Generic;

namespace Compressarr.Settings
{
    public interface ISettingsManager
    {
        Dictionary<string, string> Settings { get; }

        void AddSetting(SettingType setting, string value);
        string ConfigFile(string fileName);
        void DeleteSetting(SettingType setting);
        string GetSetting(SettingType setting);
        bool HasSetting(SettingType setting);
    }
}