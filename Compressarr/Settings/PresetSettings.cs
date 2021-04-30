using Compressarr.FFmpegFactory.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public class PresetSettings
    {
        public HashSet<FFmpegPreset> Presets { get; set; }
    }
}
