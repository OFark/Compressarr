using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    public class SystemStatus
    {
        /// <summary>
        /// Not part of the API
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Not part of the API
        /// </summary>
        public string ErrorMessage { get; set; }

        public string version { get; set; }
        public DateTime buildTime { get; set; }
        public bool isDebug { get; set; }
        public bool isProduction { get; set; }
        public bool isAdmin { get; set; }
        public bool isUserInteractive { get; set; }
        public string startupPath { get; set; }
        public string appData { get; set; }
        public string osVersion { get; set; }
        public bool isMonoRuntime { get; set; }
        public bool isMono { get; set; }
        public bool isLinux { get; set; }
        public bool isOsx { get; set; }
        public bool isWindows { get; set; }
        public string branch { get; set; }
        public string authentication { get; set; }
        public string sqliteVersion { get; set; }
        public string urlBase { get; set; }
        public string runtimeVersion { get; set; }
    }
}