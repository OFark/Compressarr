using Compressarr.FFmpegFactory.Interfaces;
using Compressarr.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class Libx264 : FFmpegPreset, IFFmpegPreset
    {
        public float? crf { get; set; }
        public h26xPreset preset { get; set; }
        public string tune { get; set; }
        public string parameters { get; set; }

        public Libx264()
        {
            crf = 23;
            preset = h26xPreset.slow;
        }

        public override List<string> GetArgumentString()
        {
            var args = base.GetArgumentString();

            var tuneStr = string.IsNullOrEmpty(tune) ? "" : $" -tune {tune}";

            if (args.Count == 1)
            {
                var insertpoint = args[0].IndexOf("libx264") + 7;

                var crfStr = crf.HasValue ? $" -crf {crf.Value}" : "";

                var paramStr = string.IsNullOrEmpty(parameters) ? "" : $" -x264-params {parameters.Trim()}";

                args[0] = args[0].Insert(insertpoint, $"{crfStr} -preset {preset}{tuneStr}{paramStr}");
            }
            else
            {
                var insertpoint = args[0].IndexOf("libx264") + 7;

                var paramStr = string.IsNullOrEmpty(parameters) ? "" : $" -x264-params {parameters}";

                args[0] = args[0].Insert(insertpoint, $" -preset {preset}{tuneStr}{paramStr}");

                insertpoint = args[1].IndexOf("libx264") + 7;

                args[1] = args[1].Insert(insertpoint, $" -preset {preset}{tuneStr}{paramStr}");
            }

            return args;
        }
    }
}