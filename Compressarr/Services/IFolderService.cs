using Compressarr.Services.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public interface IFolderService
    {
        Task<ServiceResult<IEnumerable<FileInfo>>> RequestFilesAsync(string path);
        Task<SystemStatus> TestConnectionAsync(string path);
    }
}