﻿
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Compressarr.Settings
{

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SettingType
    {
        RadarrURL,
        RadarrAPIKey,
        SonarrURL,
        SonarrAPIKey
    }
}