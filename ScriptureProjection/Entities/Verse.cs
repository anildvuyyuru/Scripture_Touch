using System.Text.Json.Serialization;

namespace ScriptureProjection.Entities
{
    public class Verse
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = "";
        public string Text { get; set; } = "";
        public string? Bg { get; set; }
    }
}
