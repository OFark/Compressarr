namespace Compressarr.Settings
{
    public class APISettings
    {
        public string APIURL { get; set; }
        public string APIKey { get; set; }
        public string BasePath { get; set; }

        public int? MaxDegreeOfParallelism { get; set; }

        public bool Ok => !string.IsNullOrWhiteSpace(APIKey) && !string.IsNullOrWhiteSpace(APIURL);
    }
}
