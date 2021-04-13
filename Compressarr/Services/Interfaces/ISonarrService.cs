using Compressarr.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Interfaces
{
    public interface ISonarrService
    {
        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey);
    }
}