using System.Threading.Tasks;

namespace Compressarr.Application
{
    public interface IFileService
    {
        string ConfigDirectory { get; }
        string FFMPEGPath { get; }
        string FFPROBEPath { get; }

        void DeleteFile(string filePath);
        Task DumpDebugFile(string fileName, string content);
        string GetAppDirPath(AppDir dir);
        string GetAppFilePath(AppFile file);
        string GetFilePath(AppDir dir, string filename);
        bool HasFile(string filePath);
        bool HasFile(AppFile file);
        bool HasFile(AppDir dir, string fileName);
        Task<T> ReadJsonFileAsync<T>(AppFile file) where T : class;
        Task<T> ReadJsonFileAsync<T>(string path) where T : class;
        Task<T> ReadJsonFileAsync<T>(AppDir dir, string fileName) where T : class;
        Task<string> ReadTextFileAsync(AppFile file);
        Task<string> ReadTextFileAsync(string path);
        Task WriteJsonFileAsync(AppFile file, object content);
        Task WriteJsonFileAsync(string path, object content);
        Task WriteJsonFileAsync(AppDir dir, string fileName, object content);
        Task WriteTextFileAsync(AppFile file, string content);
        Task WriteTextFileAsync(string path, string content);
        Task WriteTextFileAsync(AppDir cache, string fileName, string content);
    }
}