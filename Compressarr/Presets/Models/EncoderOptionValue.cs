using Compressarr.Settings.FFmpegFactory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{

    public class EncoderOptionValue : EncoderOptionValueBase 
    {
        public EncoderOptionValue(string name)
        {
            Name = name;
        }
        public EncoderOptionValue(EncoderOptionValueBase valueBase)
        {
            Name = valueBase?.Name;
            Value = valueBase?.Value;
        }

        public int? IntValue
        {
            get
            {
                return int.TryParse(Value, out var x) ? x : null;
            }
            set
            {
                Value = value.ToString();
            }
        }
        public EncoderOption EncoderOption { get; set; }
    }

}
