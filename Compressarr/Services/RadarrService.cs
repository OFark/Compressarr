using Compressarr.FFmpegFactory;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Application;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Compressarr.Settings;

namespace Compressarr.Services
{
    public class RadarrService : IRadarrService
    {

        private readonly IFFmpegManager fFmpegManager;
        private readonly IFileService fileService;
        private readonly ILogger<RadarrService> logger;
        private readonly IApplicationService applicationService;

        private StatusResult _status = null;
        
        private HashSet<Movie> movies = new();
        private int previousResultsHash;

        public RadarrService(IFFmpegManager fFmpegManager, IFileService fileService, ILogger<RadarrService> logger, IApplicationService applicationService)
        {
            this.fFmpegManager = fFmpegManager;
            this.fileService = fileService;
            this.logger = logger;
            this.applicationService = applicationService;
        }

        public event EventHandler OnUpdate;

        public long MovieCount => movies?.Count() ?? 0;
        public string MovieFilter { get; set; }
        public IEnumerable<string> MovieFilterValues { get; set; }
        public void ClearCache()
        {
            movies = new();
        }
        public async Task<HashSet<Movie>> GetMoviesAsync(bool force = false)
        {
            using (logger.BeginScope("Get Movies"))
            {
                logger.LogInformation($"Fetching Movies");

                if (!movies.Any() || force)
                {
                    var moviesRequest = await RequestMovies();
                    if (moviesRequest.Success)
                    {
                        movies = moviesRequest.Results;
                    }
                    else
                    {
                        return new();
                    }
                }

                if (!string.IsNullOrWhiteSpace(MovieFilter))
                {
                    logger.LogDebug("Filtering Movies");
                    logger.LogDebug($"Filter: {MovieFilter}");
                    logger.LogDebug($"Filter Values: {string.Join(", ", MovieFilterValues)}");

                    return movies.AsQueryable().Where(MovieFilter, MovieFilterValues.ToArray()).ToHashSet();
                }

                return movies;
            }
        }

        public async Task<ServiceResult<HashSet<Movie>>> GetMoviesFilteredAsync(string filter, IEnumerable<string> filterValues)
        {
            using (logger.BeginScope("Get Filtered Movies"))
            {
                logger.LogDebug("Filtering Movies");
                logger.LogDebug($"Filter: {filter}");
                logger.LogDebug($"Filter Values: {string.Join(", ", filterValues)}");
                var movies = await RequestMovies();

                if (movies.Success)
                {
                    return new(true, movies.Results.AsQueryable().Where(filter, filterValues.ToArray()).ToHashSet());
                }
                else
                {
                    return movies;
                }
            }
        }

        public async Task<StatusResult> GetStatus()
        {
            if (_status == null)
            {
                _status = new();

                if (!string.IsNullOrWhiteSpace(applicationService.RadarrSettings?.APIURL))
                {
                    if ((await TestConnection(applicationService.RadarrSettings)).Success)
                    {
                        _status.Status = ServiceStatus.Ready;
                        _status.Message = new("Ready");
                    }
                    else
                    {
                        _status.Status = ServiceStatus.Partial;
                        _status.Message = new("Connection details are available, but connection cannot be completed. Check <a href=\"/options\">options</a>");
                    }
                }
                else
                {
                    _status.Status = ServiceStatus.Incomplete;
                    _status.Message = new("Connection details are missing. Use the <a href=\"/options\">options</a> page to enter them");

                }
            }

            return _status;
        }

        public async Task<ServiceResult<List<string>>> GetValuesForProperty(string property)
        {
            using (logger.BeginScope("Get Values for Property"))
            {
                logger.LogDebug($"Property name: {property}");


                if (movies.Any())
                {
                    return new ServiceResult<List<string>>(true, movies.AsQueryable().GroupBy(property).OrderBy("Count() desc").ThenBy("Key").Select("Key").ToDynamicArray<string>().Where(x => !string.IsNullOrEmpty(x)).ToList());
                }

                var moviesResult = await RequestMovies();
                return new ServiceResult<List<string>>(moviesResult.Success, moviesResult.ErrorCode, moviesResult.ErrorMessage);
            }
        }

        public async Task<ServiceResult<object>> ImportMovie(WorkItem workItem)
        {
            //Get ManualImport 
            //Request URL: /api/manualimport?folder=C%3A%5CCompressarr%5C&filterExistingFiles=true

            //Do Import
            //Request URL: /api/v3/command - /api/command may also work
            //FormData: {"name":"ManualImport",
            //       "files":[{
            //           "path":"C:\\Compressarr\\Leroy & Stitch (2006)\\Leroy & Stitch (2006) SDTV.mkv",
            //           "folderName":"Leroy & Stitch (2006)",
            //           "movieId":1,
            //           "quality":{
            //               "quality":{
            //                   "id":1,
            //           "name":"SDTV",
            //           "source":"tv",
            //           "resolution":480,
            //           "modifier":"none"
            //                   },
            //        "revision":{
            //                   "version":1,
            //           "real":0,
            //           "isRepack":false
            //           }
            //           },
            //    "languages":[{ "id":1,"name":"English"}]}],
            //    "importMode":"auto"}

            //Refresh
            //Request URL: /api/v3/command - /api/command may also work
            //FormData: {"name":"RefreshMovie","movieIds":[1]}:

            using (logger.BeginScope("Import Movie"))
            {
                logger.LogInformation("Asking Radarr to validate imports");

                var destinationFolder = Path.GetDirectoryName(workItem.DestinationFile);

                var link = $"{applicationService.RadarrSettings?.APIURL}/api/manualimport?folder={HttpUtility.UrlEncode(destinationFolder)}&filterExistingFiles=true&apikey={applicationService.RadarrSettings?.APIKey}";
                logger.LogDebug($"Link: {link}");

                ManualImportResponse mir = null;

                using (var hc = new HttpClient())
                {
                    try
                    {
                        logger.LogDebug("Requesting ManualImport");

                        var hrm = await hc.GetAsync(link);

                        var manualImportJSON = await hrm.Content.ReadAsStringAsync();

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("manualImport.json", manualImportJSON).ConfigureAwait(false);
                        }

                        if (hrm.IsSuccessStatusCode)
                        {
                            var mirs = JsonConvert.DeserializeObject<HashSet<ManualImportResponse>>(manualImportJSON);

                            mir = mirs.FirstOrDefault(x => x.movie.id == workItem.SourceID && x.relativePath == workItem.DestinationFileName);

                            if (mir == null)
                            {
                                logger.LogWarning("Failed: Radarr didn't recognise the file to import");
                                return new(false, "Failed", "Radarr didn't recognise the file to import");
                            }

                            logger.LogInformation($"Success.");
                        }
                        else
                        {
                            logger.LogWarning($"Failed: {hrm.StatusCode}");
                            if (hrm.ReasonPhrase != hrm.StatusCode.ToString())
                            {
                                logger.LogWarning($"Failed: {hrm.ReasonPhrase}");
                                return new(false, "Failed", hrm.ReasonPhrase);
                            }

                            return new(false, "Failed", hrm.StatusCode.ToString());
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                        return new ServiceResult<object>(false, "Exception", ex.ToString());
                    }
                }

                if (mir.rejections.Any())
                {
                    logger.LogWarning($"Radarr rejection [{mir.rejections.FirstOrDefault().type}] : {mir.rejections.FirstOrDefault().reason}");

                    return new(false, $"Radarr rejection: {mir.rejections.FirstOrDefault().type}", mir.rejections.FirstOrDefault().reason);
                }

                logger.LogInformation("Importing into Radarr");

                var payload = new ImportMoviePayload();


                logger.LogDebug("Get FileInfo");

                var encodedFile = new FileInfo(workItem.DestinationFile);

                var file = new ImportMoviePayload.File()
                {
                    folderName = encodedFile.Directory.Name,
                    path = mir.path,
                    movieId = workItem.SourceID,
                    quality = mir.quality,
                    languages = new() { new Language() { id = 1, name = "English" } }
                };

                payload.files = new() { file };

                link = $"{applicationService.RadarrSettings?.APIURL}/api/command?apikey={applicationService.RadarrSettings?.APIKey}";
                logger.LogDebug($"Link: {link}");


                using (var hc = new HttpClient())
                {
                    try
                    {
                        logger.LogDebug("POSTing payload");

                        var payloadJson = JsonConvert.SerializeObject(payload);

                        var result = await hc.PostAsync(link, new StringContent(payloadJson, Encoding.UTF8, "application/json"));

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("importMoviePayload.json", payloadJson);
                            _ = fileService.DumpDebugFile("importMovieResponse.json", await result.Content.ReadAsStringAsync());
                        }

                        if (result.IsSuccessStatusCode)
                        {
                            logger.LogDebug("Success");
                            ClearCache();
                            return new ServiceResult<object>(true, true);
                        }
                        else
                        {
                            logger.LogWarning($"Failed: {result.ReasonPhrase}");
                            _ = fileService.DumpDebugFile("importMovieResponse.json", await result.Content.ReadAsStringAsync());
                            return new ServiceResult<object>(false, result.StatusCode.ToString(), result.ReasonPhrase);
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                        return new ServiceResult<object>(false, "Exception", ex.ToString());
                    }
                }
            }
        }

        public async Task<SystemStatus> TestConnection(APISettings settings)
        {
            using (logger.BeginScope("Test Connection"))
            {
                if (settings == null || !settings.Ok)
                {
                    logger.LogDebug("Test aborted, due to insufficient settings");
                    return new() { Success = false, ErrorMessage = "Radarr Settings are missing" };
                }
                logger.LogInformation($"Test Radarr Connection.");
                SystemStatus ss = new();

                var link = $"{settings.APIURL}/api/system/status?apikey={settings.APIKey}";

                logger.LogDebug($"LinkURL: {link}");
                var hc = new HttpClient();
                try
                {
                    logger.LogDebug($"Connecting.");
                    var hrm = await hc.GetAsync(link);

                    var statusJSON = await hrm.Content.ReadAsStringAsync();
                    if (AppEnvironment.IsDevelopment)
                    {
                        _ = fileService.DumpDebugFile("testConnection.json", statusJSON);
                    }

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

        private async Task<ServiceResult<HashSet<Movie>>> RequestMovies()
        {
            using (logger.BeginScope("Requesting Movies"))
            {
                if (applicationService.RadarrSettings == null)
                {
                    logger.LogWarning($"No Radarr settings.");
                    return new(false, "404", "Radarr settings not found. In Options. Go there");
                }

                var radarrURL = applicationService.RadarrSettings?.APIURL;// settingsManager.Settings[SettingType.RadarrURL];
                var radarrAPIKey = applicationService.RadarrSettings?.APIKey; // settingsManager.Settings[SettingType.RadarrAPIKey];

                var link = $"{radarrURL}/api/movie?apikey={radarrAPIKey}";
                logger.LogDebug($"Link: {link}");

                if (string.IsNullOrWhiteSpace(radarrURL))
                {
                    logger.LogWarning($"No URL for Radarr.");
                    return new(false, "404", "Radarr URL not found. In Options. Go there");
                }

                if (string.IsNullOrWhiteSpace(radarrAPIKey))
                {
                    logger.LogWarning($"No API key for Radarr.");
                    return new(false, "404", "Radarr APIKey not found. In Options. Go there");
                }

                var movieJSON = string.Empty;

                try
                {
                    logger.LogDebug($"Creating new HTTP client.");
                    using (var hc = new HttpClient())
                    {
                        logger.LogDebug($"Downlading Movie List.");
                        movieJSON = await hc.GetStringAsync(link);

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("movies.json", movieJSON);
                        }

                        if (previousResultsHash != 0)
                        {
                            var newHash = movieJSON.GetHashCode();
                            if (newHash != previousResultsHash)
                            {
                                //snackbar.Add("Movie List updated", Severity.Info);
                                previousResultsHash = newHash;
                            }
                        }
                        else
                        {
                            previousResultsHash = movieJSON.GetHashCode();
                        }

                        var moviesArr = JsonConvert.DeserializeObject<Movie[]>(movieJSON);

                        ServiceResult<HashSet<Movie>> getMoviesResult = new(true, moviesArr.Where(m => m.downloaded && m.movieFile != null && m.movieFile.mediaInfo != null).OrderBy(m => m.title).ToHashSet(), new(0, 1, 0));

                        if (applicationService.LoadMediaInfoOnFilters)
                        {
                            _ = Task.Run(async () =>
                            {
                                foreach (var movie in getMoviesResult.Results)
                                {
                                    movie.MediaInfo = await fFmpegManager.GetMediaInfoAsync(Path.Combine(movie.path, movie.movieFile.relativePath));
                                    OnUpdate?.Invoke(this, null);
                                }
                            });
                        }

                        logger.LogDebug($"Success.");
                        return getMoviesResult;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        logger.LogError($"{ex}");

                        logger.LogWarning($"Error understanding output from Radarr. Dumping output to: movie.json");

                        await File.WriteAllTextAsync("movie.json", movieJSON);
                    }
                    catch (Exception)
                    {
                        logger.LogCritical("Cannot dump debug file, permissions?");
                        logger.LogCritical(ex.ToString());
                    }

                    return new(false, null, ex.Message);
                }
            }
        }
    }
}