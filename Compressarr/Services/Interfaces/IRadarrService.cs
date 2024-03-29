﻿using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Application;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Compressarr.Settings;

namespace Compressarr.Services
{
    public interface IRadarrService : IJobDependency
    {
        public long MovieCount { get; }

        string MovieFilter { get; set; }

        IEnumerable<string> MovieFilterValues { get; set; }

        void ClearCache();


        Task<ServiceResult<IEnumerable<Movie>>> GetMoviesAsync(bool force = false);

        Task<ServiceResult<IEnumerable<Movie>>> GetMoviesFilteredAsync(string filter, IEnumerable<string> filterValues);

        public Task<ServiceResult<List<string>>> GetValuesForPropertyAsync(string property);

        Task<ServiceResult<object>> ImportMovieAsync(WorkItem workItem);
        Task<ServiceResult<IEnumerable<Movie>>> RequestMoviesFilteredAsync(string filter, IEnumerable<string> filterValues);

        public Task<SystemStatus> TestConnectionAsync(APISettings settings);
    }
}