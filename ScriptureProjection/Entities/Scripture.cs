namespace ScriptureFlow_Touch.Entities
{
    public class Scripture
    {
        public string Version { get; set; } = "";
        public Dictionary<string, string> Backgrounds { get; set; } = [];
        public Dictionary<string, List<Reference>> Groups { get; set; } = [];
    }
}
