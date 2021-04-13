using Compressarr.Services.Interfaces;
using Compressarr.Services.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using System.Security.Cryptography.X509Certificates;
using Compressarr.Settings;

namespace Compressarr.Services
{
    public class SonarrService : ISonarrService
    {
        private SettingsManager settingsManager;

        public SonarrService(SettingsManager _settingsManager)
        {
            settingsManager = _settingsManager;
        }

        public SystemStatus TestConnection(string sonarrURL, string sonarrAPIKey)
        {
            SystemStatus ss = new SystemStatus();

            var link = $"{sonarrURL}/api/system/status?apikey={sonarrAPIKey}";
            string statusJSON = null;
            HttpResponseMessage hrm = null;

            var hc = new HttpClient();
            try
            {
                hrm = hc.GetAsync(link).Result;

                statusJSON = hrm.Content.ReadAsStringAsync().Result;

                if (hrm.IsSuccessStatusCode)
                {
                    ss = JsonConvert.DeserializeObject<SystemStatus>(statusJSON);
                    ss.Success = true;
                }
                else
                {
                    ss.Success = false;
                    ss.ErrorMessage = $"{hrm.StatusCode}";
                    if (hrm.ReasonPhrase != hrm.StatusCode.ToString())
                    {
                        ss.ErrorMessage += $"- {hrm.ReasonPhrase}";
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException)
            {
                ss.Success = false;

                ss.ErrorMessage = $"Request Exception: {ex.Message}";
            }
            catch (JsonReaderException)
            {
                ss.Success = false;
                ss.ErrorMessage = $"Response wasn't a valid API response. This is usually due to an incorrect URL";
            }

            return ss;
        }
    }
}