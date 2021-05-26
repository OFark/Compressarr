using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class AutoPresetTest
    {
        public AutoPresetTest(string arg)
        {
            Argument = arg;
            AutoPresetResultSet = new();
        }

        public string Argument { get; set; }

        public Dictionary<string, AutoPresetResult> AutoPresetResultSet { get; set; }
    }
}
