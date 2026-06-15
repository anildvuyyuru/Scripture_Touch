using ScriptureFlow_Touch.Interfaces;
using System.Text.Json;

namespace ScriptureFlow_Touch.Services
{
    public class FileService : IFileService
    {
        public string ReadText(string path) => File.ReadAllText(path);

        public bool FileExists(string? path) => File.Exists(path);

        public string[] GetImageFiles(string folder)
        {
            return Directory.GetFiles(folder, "*.*")
                .Where(p => p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public bool IsValidJson(string json)
        {
            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

    }
}
