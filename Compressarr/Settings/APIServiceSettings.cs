using Compressarr.FFmpegFactory.Models;
using Compressarr.Filtering.Models;
using Compressarr.JobProcessing.Models;
using System.Collections.Generic;

namespace Compressarr.Settings
{
    public class APIServiceSettings
    {
        public APISettings RadarrSettings { get; set; }
        public APISettings SonarrSettings { get; set; }
    }
}
