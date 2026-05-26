using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace CodexLocalRetrieval.Core.Models;

public sealed class AppStoreData
{
    [JsonPropertyName("sessions")]
    public Dictionary<string, ArchiveSession> Sessions { get; set; } = new();

    [JsonPropertyName("settings")]
    public ArchiveSettings Settings { get; set; } = new();

    [JsonPropertyName("collections")]
    public Dictionary<string, ArchiveCollection> Collections { get; set; } = new();
}

public sealed class ArchiveSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "amoled";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "rose";

    [JsonPropertyName("accentHex")]
    public string AccentHex { get; set; } = "#fb7185";

    [JsonPropertyName("radius")]
    public string Radius { get; set; } = "compact";

    [JsonPropertyName("density")]
    public string Density { get; set; } = "comfortable";

    [JsonPropertyName("readOnlySourceMode")]
    public bool ReadOnlySourceMode { get; set; } = true;

    [JsonPropertyName("panelRadius")]
    public int PanelRadius { get; set; } = 12;

    [JsonPropertyName("controlRadius")]
    public int ControlRadius { get; set; } = 12;

    [JsonPropertyName("activeAiProviderId")]
    public string ActiveAiProviderId { get; set; } = "deepseek";

    [JsonPropertyName("aiProviders")]
    public List<AiProviderSettings> AiProviders { get; set; } = new();
}

public sealed class AiProviderSettings
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "openai-compatible";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class ArchiveCollection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sessionIds")]
    public List<string> SessionIds { get; set; } = new();

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#fb7185";
}

public sealed class ArchiveSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("customTitle")]
    public string CustomTitle { get; set; } = "";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("workspace")]
    public string Workspace { get; set; } = "";

    [JsonPropertyName("workspaceName")]
    public string WorkspaceName { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public ObservableCollection<ArchiveMessage> Messages { get; set; } = new();

    [JsonPropertyName("codeBlocks")]
    public ObservableCollection<CodeBlock> CodeBlocks { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("tags")]
    public ObservableCollection<string> Tags { get; set; } = new();

    [JsonPropertyName("reviewed")]
    public bool Reviewed { get; set; }

    [JsonPropertyName("starred")]
    public bool Starred { get; set; }

    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(CustomTitle) ? Title : CustomTitle;

    [JsonIgnore]
    public string PinGlyph => Pinned ? "*" : "";

    [JsonIgnore]
    public string DisplayDate => DateTime.TryParse(UpdatedAt, out var date) ? date.ToString("MMM d") : "";
}

public sealed class ArchiveMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("codeBlocks")]
    public ObservableCollection<CodeBlock> CodeBlocks { get; set; } = new();

    [JsonIgnore]
    public string RoleLabel => string.IsNullOrWhiteSpace(Role) ? "Message" : char.ToUpper(Role[0]) + Role[1..];
}

public sealed class CodeBlock
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "text";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
}

public sealed class ArchiveSearchHit
{
    public ArchiveSession Session { get; set; } = new();

    public ArchiveMessage? Message { get; set; }

    public string Snippet { get; set; } = "";

    public string SourceLabel { get; set; } = "";

    public string MatchedTerms { get; set; } = "";

    public int Score { get; set; }
}
