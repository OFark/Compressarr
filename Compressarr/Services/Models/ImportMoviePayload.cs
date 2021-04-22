using System.Collections.Generic;

namespace Compressarr.Services.Models
{
    public class ImportMoviePayload
    {
        public enum ImportMode
        {
            copy,
            move
        }

        public HashSet<File> files { get; set; }

        public ImportMode importMode { get; set; }

        public const string name = "ManualImport";
        public class File
        {
            public string folderName { get; set; }
            public List<Language> languages { get; set; }
            public int movieId { get; set; }
            public string path { get; set; }
            public Quality quality { get; set; }
        }

        public class Language
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        public class Quality2
        {
            public int id { get; set; }
            public string modifier { get; set; }
            public string name { get; set; }
            public Quality quality { get; set; }
            public int resolution { get; set; }
            public Revision revision { get; set; }
            public string source { get; set; }
        }

        public class Revision
        {
            public bool isRepack { get; set; }
            public int real { get; set; }
            public int version { get; set; }
        }
    }

}
