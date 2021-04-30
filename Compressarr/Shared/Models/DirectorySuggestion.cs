using System.IO;

namespace Compressarr.Shared.Models
{
    public class DirectorySuggestion
    {
        public string Name { get; set; }
        public string Suggestion { get; set; }

        public DirectorySuggestion(DirectoryInfo directoryInfo)
        {
            if (directoryInfo != null)
            {
                Name = directoryInfo.Name;
                Suggestion = directoryInfo.FullName;
            }
        }

        public DirectorySuggestion(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var directoryInfo = new DirectoryInfo(path);
                Name = directoryInfo.Name;
                Suggestion = directoryInfo.FullName;
            }
        }

        public override string ToString()
        {
            return Suggestion;
        }
    }
}
