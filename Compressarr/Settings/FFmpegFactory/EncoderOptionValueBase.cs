﻿using Compressarr.Presets.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings.FFmpegFactory
{
    public class EncoderOptionValueBase
    {
        public EncoderOptionValueBase()
        {

        }
        public EncoderOptionValueBase(EncoderOptionValue eov)
        {
            Name = eov?.Name;
            Value = eov?.Value;
            AutoCalculate = eov?.AutoCalculate ?? false;
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public bool AutoCalculate { get; set; }
    }
}
