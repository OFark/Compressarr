using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{
    public class ManualImportMovieResponse
    {
        //This is a response to the ManualImport GET not the ManualImport Command POST

        public string path { get; set; }
        public string relativePath { get; set; }
        public string name { get; set; }
        public long size { get; set; }
        public Movie movie { get; set; }
        public MovieFile.Quality quality { get; set; }
        public int qualityWeight { get; set; }
        public HashSet<Rejection> rejections { get; set; }
        public long id { get; set; }
    }
}
