using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net.Http;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public class RadarrService : IRadarrService
    {
        private readonly ILogger<RadarrService> logger;
        private readonly ISettingsManager settingsManager;

        private HashSet<Movie> cachedMovies = null;

        public RadarrService(ISettingsManager _settingsManager, ILogger<RadarrService> logger)
        {
            settingsManager = _settingsManager;
            this.logger = logger;
        }

        public long MovieCount => cachedMovies?.Count ?? 0;
        public async Task<ServiceResult<HashSet<Movie>>> GetMovies()
        {
            using (logger.BeginScope("Get Movies"))
            {
                logger.LogInformation($"Fetching Movies");

                if (cachedMovies == null)
                {
                    logger.LogDebug($"No cached movies, interrogate Radarr.");
                    var radarrURL = settingsManager.GetSetting(SettingType.RadarrURL);
                    var radarrAPIKey = settingsManager.GetSetting(SettingType.RadarrAPIKey);

                    var link = $"{radarrURL}/api/movie?apikey={radarrAPIKey}";

                    if (string.IsNullOrWhiteSpace(radarrURL))
                    {
                        logger.LogWarning($"No URL for Radarr.");
                        return new ServiceResult<HashSet<Movie>>(false, "404", "Radarr URL not found. In Options. Go there");
                    }

                    if (string.IsNullOrWhiteSpace(radarrAPIKey))
                    {
                        logger.LogWarning($"No API key for Radarr.");
                        return new ServiceResult<HashSet<Movie>>(false, "404", "Radarr APIKey not found. In Options. Go there");
                    }

                    var movieJSON = string.Empty;

                    try
                    {
                        logger.LogDebug($"Creating new HTTP client.");
                        using (var hc = new HttpClient())
                        {
                            logger.LogDebug($"Downlading Movie List.");
                            movieJSON = await hc.GetStringAsync(link);
                            var moviesArr = JsonConvert.DeserializeObject<Movie[]>(movieJSON);

                            cachedMovies = moviesArr.Where(m => m.downloaded).OrderBy(m => m.title).ToHashSet();
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            logger.LogError($"{ex}");
                            var debugFolder = Path.Combine(SettingsManager.ConfigDirectory, "debug");
                            if (!Directory.Exists(debugFolder))
                            {
                                Directory.CreateDirectory(debugFolder);
                            }

                            var movieJSONFile = Path.Combine(debugFolder, "movie.json");

                            logger.LogWarning($"Error understanding output from Radarr. Dumping output to: {movieJSONFile}");

                            await File.WriteAllTextAsync(movieJSONFile, movieJSON);
                        }
                        catch (Exception)
                        {
                            logger.LogCritical("Cannot dump debug file, permissions?");
                            logger.LogCritical(ex.ToString());
                        }

                        return new ServiceResult<HashSet<Movie>>(false, ex.Message, ex.InnerException?.ToString());
                    }
                }

                return new ServiceResult<HashSet<Movie>>(true, cachedMovies);
            }
        }

        public ServiceResult<HashSet<Movie>> GetMoviesByJSON(string json) => throw new NotImplementedException();

        public async Task<ServiceResult<HashSet<Movie>>> GetMoviesFiltered(string filter, string[] filterValues)
        {
            using (logger.BeginScope("Get Filtered Movies"))
            {
                logger.LogDebug("Filtering Movies");
                logger.LogDebug($"Filter: {filter}");
                logger.LogDebug($"Filter Values: {string.Join(", ", filterValues)}");
                var movies = await GetMovies();

                if (movies.Success)
                {
                    movies.Results = movies.Results.AsQueryable().Where(filter, filterValues).ToHashSet();
                }

                return movies;
            }
        }

        public async Task<ServiceResult<List<string>>> GetValuesForProperty(string property)
        {
            using (logger.BeginScope("Get Values for Property"))
            {
                logger.LogDebug($"Property name: {property}");

                var movies = await GetMovies();

                if (movies.Success)
                {
                    return new ServiceResult<List<string>>(true, movies.Results.AsQueryable().GroupBy(property).OrderBy("Count() desc").ThenBy("Key").Select("Key").ToDynamicArray<string>().Where(x => !string.IsNullOrEmpty(x)).ToList());
                }

                return new ServiceResult<List<string>>(movies.Success, movies.ErrorCode, movies.ErrorMessage);
            }
        }

        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey)
        {
            using (logger.BeginScope("Test Connection"))
            {

                logger.LogInformation($"Test Radarr Connection.");
                SystemStatus ss = new SystemStatus();

                var link = $"{radarrURL}/api/system/status?apikey={radarrAPIKey}";

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