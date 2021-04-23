using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public interface ISettingsManager
    {
        Dictionary<string, string> Settings { get; }

        void AddSetting(SettingType setting, string value);
        void DeleteSetting(SettingType setting);
        void DumpDebugFile(string fileName, string content);
        string GetSetting(SettingType setting);
        bool HasSetting(SettingType setting);
        T LoadSettingFile<T>(string fileName);
        void SaveSettingFile(string fileName, object content);
    }
}