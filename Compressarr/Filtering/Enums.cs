using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Filtering
{
    public enum FilterPropertyType
    {
        String,
        Number,
        Enum
    }

    public enum MediaSource
    {
        Radarr,
        Sonarr
    }
}