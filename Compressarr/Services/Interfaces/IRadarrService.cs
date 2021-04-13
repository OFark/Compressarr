﻿using Compressarr.Services.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Interfaces
{
    public interface IRadarrService
    {
        public Task<ServiceResult<HashSet<Movie>>> GetMovies();

        public Task<ServiceResult<HashSet<Movie>>> GetMoviesFiltered(string filter, string[] filterValues);

        public ServiceResult<HashSet<Movie>> GetMoviesByJSON(string json);

        public long MovieCount { get; }

        public Task<ServiceResult<List<string>>> GetValuesForProperty(string property);

        public SystemStatus TestConnection(string radarrURL, string radarrAPIKey);
    }
}