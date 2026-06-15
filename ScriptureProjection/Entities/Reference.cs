using System.Text.Json.Serialization;

namespace ScriptureFlow_Touch.Entities
{
    public class Reference
    {
        public string Ref { get; set; } = "";

        [JsonPropertyName("book_name")]
        public string Book { get; set; } = "";

        [JsonPropertyName("chapter")]
        public int Chapter { get; set; }

        [JsonPropertyName("verse")]
        public int Verse { get; set; }
        public string Text { get; set; } = "";
        public string? Bg { get; set; }
    }
}
