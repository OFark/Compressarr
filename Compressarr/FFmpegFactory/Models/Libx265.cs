using Compressarr.FFmpegFactory.Interfaces;
using System.Collections.Generic;

namespace Compressarr.FFmpegFactory.Models
{
    public class Libx265 : FFmpegPreset, IFFmpegPreset
    {
        public float? crf { get; set; }
        public h26xPreset preset { get; set; }
        public string tune { get; set; }
        public string parameters { get; set; }

        public Libx265()
        {
            crf = 28;
            preset = h26xPreset.slow;
        }

        public override List<string> GetArgumentString()
        {
            var args = base.GetArgumentString();

            var tuneStr = string.IsNullOrEmpty(tune) ? "" : $" -tune {tune}";

            if (args.Count == 1)
            {
                var insertpoint = args[0].IndexOf("libx265") + 7;

                var crfStr = crf.HasValue ? $" -crf {crf.Value}" : "";

                var paramStr = string.IsNullOrEmpty(parameters) ? "" : $" -x265-params {parameters.Trim()}";

                args[0] = args[0].Insert(insertpoint, $"{crfStr} -preset {preset}{tuneStr}{paramStr}");
            }
            else
            {
                var insertpoint = args[0].IndexOf("libx265") + 7;

                var paramStr = string.IsNullOrEmpty(parameters) ? " -x265-params " : $" -x265-params {parameters}:";

                args[0] = args[0].Insert(insertpoint, $" -preset {preset}{tuneStr}{paramStr}pass=1").Replace("-pass 1 ", "");

                insertpoint = args[1].IndexOf("libx265") + 7;

                args[1] = args[1].Insert(insertpoint, $" -preset {preset}{tuneStr}{paramStr}pass=2").Replace("-pass 2 ", "");
            }

            return args;
        }
    }
}