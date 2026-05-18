using ScriptureProjection.Entities;
using System.Text.Json.Serialization;

namespace ScriptureProjection
{
    [JsonSerializable(typeof(Scripture))]
    [JsonSerializable(typeof(List<Scripture>))]
    [JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    PropertyNameCaseInsensitive = true)]

    public partial class VerseJsonContext : JsonSerializerContext
    {
        [JsonSerializable(typeof(Scripture))]
        public partial class VerseDataContext : JsonSerializerContext { }

    }

}
