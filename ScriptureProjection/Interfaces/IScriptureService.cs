

using System.Diagnostics.CodeAnalysis;

namespace ScriptureFlow_Touch.Interfaces
{
    public interface IScriptureService
    {
        [RequiresUnreferencedCode("")]
        Task<Entities.Scripture?> LoadScripture();
        Task<Entities.Reference?> GetVerse(string group, int verseIndex);
        Task<string?> GetBackgroundPath(Entities.Reference verse, string group, Entities.Scripture verseData);
        Task<int?> ChangeFrequency(int intervalIndex, int[] intervalArray, bool isRunning);
        Task<int?> ToggleRun(int intervalIndex, int[] intervalArray, bool isRunning);
        Task<int> GetNextVerseIndex(Entities.Scripture verseData, string group, int index);
        Task<int> GetPreviousVerseIndex(Entities.Scripture verseData, string group, int index);
        Task<string> GetReferenceInfo(Entities.Scripture verseData, Entities.Reference verse);
        Task<string> GetInfo(Entities.Scripture verseData, string group, int[] intervalArray, int intervalIndex, bool isRunning);
    }
}
