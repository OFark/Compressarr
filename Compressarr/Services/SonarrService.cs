using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public class SonarrService : ISonarrService
    {

        private readonly ILogger<SonarrService> logger;

        public SonarrService(ILogger<SonarrService> logger)
        {
            this.logger = logger;
        }

        public async Task<SystemStatus> TestConnection(APISettings settings)
        {
            using (logger.BeginScope("Test Connection"))
            {

                logger.LogInformation($"Test Sonarr Connection.");
                SystemStatus ss = new SystemStatus();

                var link = $"{settings.APIURL}/api/system/status?apikey={settings.APIKey}";
                logger.LogDebug($"LinkURL: {link}");

                string statusJSON = null;
                HttpResponseMessage hrm = null;

                var hc = new HttpClient();
                try
                {
                    logger.LogDebug($"Connecting.");
                    hrm = await hc.GetAsync(link);

                    statusJSON = await hrm.Content.ReadAsStringAsync();

                    if (hrm.IsSuccessStatusCode)
                    {
                        ss = JsonConvert.DeserializeObject<SystemStatus>(statusJSON);
                        ss.Success = true;
                        logger.LogInformation($"Success.");
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
}