using Compressarr.Services.Interfaces;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.AspNetCore.Hosting;
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
        private SettingsManager settingsManager;

        private static HashSet<Movie> cachedMovies = null;

        public long MovieCount => cachedMovies == null ? 0 : cachedMovies.Count;

        private IWebHostEnvironment env;

        public RadarrService(SettingsManager _settingsManager, IWebHostEnvironment _env)
        {
            settingsManager = _settingsManager;
            env = _env;
        }

        public async Task<ServiceResult<HashSet<Movie>>> GetMovies()
        {
            if (cachedMovies == null)
            {
                var radarrURL = settingsManager.GetSetting(SettingType.RadarrURL);
                var radarrAPIKey = settingsManager.GetSetting(SettingType.RadarrAPIKey);

                var link = $"{radarrURL}/api/movie?apikey={radarrAPIKey}";

                if (string.IsNullOrWhiteSpace(radarrURL))
                {
                    return new ServiceResult<HashSet<Movie>>(false, "404", "Radarr URL not found. In settings. Go Home");
                }

                if (string.IsNullOrWhiteSpace(radarrAPIKey))
                {
                    return new ServiceResult<HashSet<Movie>>(false, "404", "Radarr APIKey not found. In settings. Go Home");
                }

                var movieJSON = string.Empty;

                try
                {
                    using (var hc = new HttpClient())
                    {
                        movieJSON = await hc.GetStringAsync(link);
                        var moviesArr = JsonConvert.DeserializeObject<Movie[]>(movieJSON);

                        cachedMovies = moviesArr.Where(m => m.downloaded).OrderBy(m => m.title).ToHashSet();
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        var debugFolder = Path.Combine(env.ContentRootPath, "debug");
                        if (!Directory.Exists(debugFolder))
                        {
                            Directory.CreateDirectory(debugFolder);
                        }

                        var movieJSONFile = Path.Combine(debugFolder, "movie.json");
                        await File.WriteAllTextAsync(movieJSONFile, movieJSON);
                    }
                    catch (Exception) { }

                    return new ServiceResult<HashSet<Movie>>(false, ex.Message, ex.InnerException?.ToString());
                }
            }

            return new ServiceResult<HashSet<Movie>>(true, cachedMovies);
        }

        public ServiceResult<HashSet<Movie>> GetMoviesByJSON(string json) => throw new NotImplementedException();

        public async Task<ServiceResult<HashSet<Movie>>> GetMoviesFiltered(string filter, string[] filterValues)
        {
            var movies = await GetMovies();

            if (movies.Success)
            {
                movies.Results = movies.Results.AsQueryable().Where(filter, filterValues).ToHashSet();
            }

            return movies;
        }

        public async Task<ServiceResult<List<string>>> GetValuesForProperty(string property)
        {
            var movies = await GetMovies();

            if (movies.Success)
            {
                return new ServiceResult<List<string>>(true, movies.Results.AsQueryable().GroupBy(property).OrderBy("Count() desc").ThenBy("Key").Select("Key").ToDynamicArray<string>().Where(x => !string.IsNullOrEmpty(x)).ToList());
            }

            return new ServiceResult<List<string>>(movies.Success, movies.ErrorCode, movies.ErrorMessage);
        }

        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey)
        {
            SystemStatus ss = new SystemStatus();

            var link = $"{radarrURL}/api/system/status?apikey={radarrAPIKey}";
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
            catch (Exception ex) //when (ex is InvalidOperationException || ex is HttpRequestException || ex is SocketException)
            {
                ss.Success = false;

                ss.ErrorMessage = $"Request Exception: {ex.Message}";
            }
            //catch (JsonReaderException)
            //{
            //    ss.Success = false;
            //    ss.ErrorMessage = $"Response wasn't a valid API response. This is usually due to an incorrect URL";
            //}

            return ss;
        }
    }
}