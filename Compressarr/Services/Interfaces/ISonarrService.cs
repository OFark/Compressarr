using Compressarr.Services.Models;

namespace Compressarr.Services.Interfaces
{
    public interface ISonarrService
    {
        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey);
    }
}