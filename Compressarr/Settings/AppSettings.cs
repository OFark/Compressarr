using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public class AppSettings
    {
        public bool AlwaysCalculateSSIM { get; set; }
        public bool CacheMediaInfo { get; set; }
        public bool LoadMediaInfoOnFilters { get; set; }
        public bool InsertNamesIntoFFmpegPreviews { get; set; }

    }
}
