using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;

namespace Compressarr.Services
{
    public class SonarrService : ISonarrService
    {
        private readonly ISettingsManager settingsManager;
        private readonly ILogger<SonarrService> logger;

        public SonarrService(ISettingsManager settingsManager, ILogger<SonarrService> logger)
        {
            this.settingsManager = settingsManager;
            this.logger = logger;
        }

        public SystemStatus TestConnection(string sonarrURL, string sonarrAPIKey)
        {
            logger.LogDebug($"Test Sonarr Connection.");
            SystemStatus ss = new SystemStatus();

            var link = $"{sonarrURL}/api/system/status?apikey={sonarrAPIKey}";
            logger.LogDebug($"LinkURL: {link}");

            string statusJSON = null;
            HttpResponseMessage hrm = null;

            var hc = new HttpClient();
            try
            {
                logger.LogDebug($"Connecting.");
                hrm = hc.GetAsync(link).Result;

                statusJSON = hrm.Content.ReadAsStringAsync().Result;

                if (hrm.IsSuccessStatusCode)
                {
                    ss = JsonConvert.DeserializeObject<SystemStatus>(statusJSON);
                    ss.Success = true;
                    logger.LogDebug($"Success.");
                }
                else
                {
                    logger.LogWarning($"Failed: {hrm.StatusCode}");
                    ss.Success = false;
                    ss.ErrorMessage = $"{hrm.StatusCode}";
                    if (hrm.ReasonPhrase != hrm.StatusCode.ToString())
                    {
                        ss.ErrorMessage += $"- {hrm.ReasonPhrase}";
                        logger.LogWarning($"Failed: {hrm.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                ss.Success = false;

                ss.ErrorMessage = $"Request Exception: {ex.Message}";
                logger.LogError(ex.ToString());
            }

            return ss;
        }
    }
}