using System.Collections.Generic;

namespace Compressarr.Presets.Models
{
    public class GetArgumentsResult
    {

        public GetArgumentsResult(List<string> arguments)
        {
            Arguments = arguments;
            Success = true;

        }

        public GetArgumentsResult(bool success, List<string> arguments = null)
        {
            Success = success;
            Arguments = arguments;
        }

        public bool Success { get; set; }
        public List<string> Arguments { get; set; }
    }
}
