using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Compressarr.Services.Models
{
    public class ImportMoviePayload
    {
        public HashSet<File> files { get; set; }

        public string importMode = "auto";

        public string name = "ManualImport";

        public class File
        {
            public string folderName { get; set; }
            public List<Language> languages { get; set; }
            public int movieId { get; set; }
            public string path { get; set; }
            public MovieFile.MovieQuality quality { get; set; }
        }
    }

}
