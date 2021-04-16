using Compressarr.Services.Models;

namespace Compressarr.Services
{
    public interface ISonarrService
    {
        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey);
    }
}