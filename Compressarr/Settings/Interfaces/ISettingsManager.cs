using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public interface ISettingsManager
    {
        Dictionary<string, string> Settings { get; }

        void AddSetting(SettingType setting, string value);
        void DeleteSetting(SettingType setting);
        string GetSetting(SettingType setting);
        bool HasSetting(SettingType setting);
        Task<T> LoadSettingFile<T>(string fileName);
        Task SaveSettingFile(string fileName, object content);
    }
}