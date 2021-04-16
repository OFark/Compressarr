using System.IO;

namespace Compressarr.Shared.Models
{
    public class DirectorySuggestion
    {
        public string Name { get; set; }
        public string Suggestion { get; set; }

        public DirectorySuggestion(DirectoryInfo directoryInfo)
        {
            Name = directoryInfo.Name;
            Suggestion = directoryInfo.FullName;
        }
    }
}
