namespace ScriptureProjection.Entities
{
    public class VerseData
    {
        public string Version { get; set; } = "";
        public Dictionary<string, string> Backgrounds { get; set; } = [];
        public Dictionary<string, List<Verse>> Groups { get; set; } = [];
    }
}
