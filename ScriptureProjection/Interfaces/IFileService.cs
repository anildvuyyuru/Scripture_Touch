namespace ScriptureFlow_Touch.Interfaces
{
    public interface IFileService
    {
        string ReadText(string path);
        bool FileExists(string? path);
        string[] GetImageFiles(string folder);
        bool IsValidJson(string json);
    }
}
