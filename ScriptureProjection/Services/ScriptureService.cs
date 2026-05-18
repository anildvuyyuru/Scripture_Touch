using Microsoft.Extensions.DependencyInjection;
using ScriptureProjection.Entities;
using ScriptureProjection.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace ScriptureProjection.Services
{
    public class ScriptureService : IScriptureService
    {
        private readonly IFileService _fileService;

        private Scripture? _cachedScripture;
        private readonly object _cacheLock = new();

        public ScriptureService() : this(StartUp
            .BuildContainer()
            .BuildServiceProvider())
        {

        }

        public ScriptureService(IServiceProvider serviceProvider)
        {
            try
            {
                _fileService = serviceProvider.GetRequiredService<IFileService>();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FATAL ERROR during instantiation! {ex}");
                throw;
            }

        }

        public Task<int?> ChangeFrequency(int intervalIndex, int[] intervalArray, bool isRunning)
        {
            Console.WriteLine($"Changing frequency. New index: {intervalIndex}, isRunning: {isRunning}");

            var newInterval = intervalArray[intervalIndex] * 1000;
            return Task.FromResult<int?>(isRunning ? newInterval : null);
        }

        public Task<string?> GetBackgroundPath(Reference verse, string group, Scripture scripture)
        {
            // 1. Verse-specific background
            if (!string.IsNullOrWhiteSpace(verse?.Bg))
                return Task.FromResult<string?>(verse.Bg);

            // 2. Group folder background
            if (!scripture.Backgrounds.TryGetValue(group, out var folder) ||
                string.IsNullOrWhiteSpace(folder) ||
                !Directory.Exists(folder))
            {
                return Task.FromResult<string?>(null);
            }

            var files = _fileService.GetImageFiles(folder);
            if (files == null || files.Length == 0)
                return Task.FromResult<string?>(null);

            // Random selection (cheap enough here)
            var bg = files[Random.Shared.Next(files.Length)];
            return Task.FromResult<string?>(bg);
        }

        public Task<string> GetInfo(Scripture scripture, string group, int[] intervalArray,
            int intervalIndex, bool isRunning)
        {
            // Accessing list ensures group exists; throws early if misconfigured
            _ = scripture.Groups[group];

            var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(group);
            var info = $"• {title}";
            if (isRunning)
                info += $" every {intervalArray[intervalIndex]}s";

            return Task.FromResult(info);
        }

        public Task<int> GetNextVerseIndex(Scripture scripture, string group, int index)
        {
            var list = scripture.Groups[group];
            var nextVerseIndex = (index + 1) % list.Count;
            return Task.FromResult(nextVerseIndex);
        }

        public Task<int> GetPreviousVerseIndex(Scripture scripture, string group, int index)
        {
            var list = scripture.Groups[group];
            var prevVerseIndex = (index - 1 + list.Count) % list.Count;
            return Task.FromResult(prevVerseIndex);
        }

        public Task<string> GetReferenceInfo(Scripture scripture, Reference reference)
        {
            var scriptureReference = $"{reference.Book} {reference.Chapter}:{reference.Verse}";
            var result = string.IsNullOrWhiteSpace(scripture.Version)
                ? scriptureReference
                : $"{scriptureReference} • {scripture.Version}";

            return Task.FromResult(result);
        }

        public async Task<Reference?> GetVerse(string group, int verseIndex)
        {
            var scripture = await LoadScripture();
            if (scripture == null)
                return null;

            if (!scripture.Groups.TryGetValue(group, out var list))
                return null;

            if (verseIndex < 0 || verseIndex >= list.Count)
                return null;

            return list[verseIndex];
        }

        public async Task<Scripture?> LoadScripture()
        {
            // In-memory cache to avoid repeated disk + JSON work
            if (_cachedScripture != null)
                return _cachedScripture;

            lock (_cacheLock)
            {
                if (_cachedScripture != null)
                    return _cachedScripture;
            }

            var versesPath = Path.Combine(AppContext.BaseDirectory, "Scripture.json");
            var isFileExists = _fileService.FileExists(versesPath);
            if (!isFileExists)
                return null;

            var versesJson = _fileService.ReadText(versesPath);
            if (string.IsNullOrWhiteSpace(versesJson))
                return null;

            if (!_fileService.IsValidJson(versesJson))
                return null;

            var scripture = JsonSerializer.Deserialize(versesJson, VerseJsonContext.Default.Scripture);

            lock (_cacheLock)
            {
                _cachedScripture ??= scripture;
            }

            return await Task.FromResult(_cachedScripture);
        }

        public Task<int?> ToggleRun(int intervalIndex, int[] intervalArray, bool isRunning)
        {
            // ScriptureService no longer controls the timer; it just reports the interval.
            // The window decides whether to start/stop the timer.
            var nowRunning = !isRunning;
            int? interval = nowRunning ? intervalArray[intervalIndex] * 1000 : null;
            return Task.FromResult<int?>(interval);
        }
    }
}
