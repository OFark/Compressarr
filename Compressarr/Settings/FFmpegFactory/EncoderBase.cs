using Compressarr.Presets.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings.FFmpegFactory
{
    public class EncoderBase 
    {
        public EncoderBase()
        {

        }
        public EncoderBase(Encoder encoder)
        {
            Name = encoder?.Name;
        }

        public string Name { get; set; }
    }
}
