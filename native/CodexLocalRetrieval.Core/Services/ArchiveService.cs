using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexLocalRetrieval.Core.Models;
using Microsoft.Data.Sqlite;

namespace CodexLocalRetrieval.Core.Services;

public sealed class ArchiveService
{
    private readonly string _contentRootPath;
    private readonly string _seedStorePath;
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public AppStoreData Store { get; private set; } = new();
    public ObservableCollection<ArchiveSession> Sessions { get; } = new();

    public ArchiveService(string? storePath = null, string? contentRootPath = null)
    {
        _contentRootPath = contentRootPath ?? FindProjectRoot();
        _seedStorePath = Path.Combine(_contentRootPath, "data", "app-store.json");
        _storePath = storePath ?? DefaultStorePath();
    }

    public async Task LoadAsync()
    {
        if (File.Exists(_storePath))
        {
            var json = await File.ReadAllTextAsync(_storePath);
            Store = JsonSerializer.Deserialize<AppStoreData>(json, _jsonOptions) ?? new AppStoreData();
        }
        else if (File.Exists(_seedStorePath))
        {
            var json = await File.ReadAllTextAsync(_seedStorePath);
            Store = JsonSerializer.Deserialize<AppStoreData>(json, _jsonOptions) ?? new AppStoreData();
        }
        NormalizeSettings();
        RefreshSessions(OrderedVisibleSessions(Store.Sessions.Values));
    }

    public string ResolveDefaultChatRoot()
    {
        if (!string.IsNullOrWhiteSpace(Store.Settings.ChatRootPath) && Directory.Exists(Store.Settings.ChatRootPath))
        {
            return Store.Settings.ChatRootPath;
        }

        return CandidateChatRoots().FirstOrDefault(root => Directory.Exists(root) && ContainsSessionJsonl(root)) ?? "";
    }

    public IReadOnlyList<string> CandidateChatRoots()
    {
        var roots = new List<string>();
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            AddIfUnique(roots, Path.Combine(codexHome, "sessions"));
            AddIfUnique(roots, Path.Combine(codexHome, "archived_sessions"));
            AddIfUnique(roots, codexHome);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userCodex = Path.Combine(home, ".codex");
        AddIfUnique(roots, Path.Combine(userCodex, "sessions"));
        AddIfUnique(roots, Path.Combine(userCodex, "archived_sessions"));
        AddIfUnique(roots, Path.Combine(userCodex, "repair-backups"));
        AddIfUnique(roots, userCodex);
        return roots;
    }

    public async Task<int> AutoIndexAsync(IProgress<string>? progress = null)
    {
        if (!Store.Settings.AutoIndexOnStartup)
        {
            progress?.Report("Auto-index is off.");
            return 0;
        }

        var root = ResolveDefaultChatRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            Store.Settings.LastIndexStatus = "No local Codex sessions were found. Set a chat folder in Settings.";
            progress?.Report(Store.Settings.LastIndexStatus);
            await SaveAsync();
            return 0;
        }

        if (!HasOnlyDemoSessions()
            && !string.IsNullOrWhiteSpace(Store.Settings.LastIndexedAt)
            && string.Equals(Store.Settings.ChatRootPath, root, StringComparison.OrdinalIgnoreCase))
        {
            Store.Settings.LastIndexStatus = $"Using existing index from {Store.Settings.ChatRootPath}. Use Index folder to refresh.";
            progress?.Report(Store.Settings.LastIndexStatus);
            return 0;
        }

        progress?.Report($"Indexing {root}");
        return await IndexRootAsync(root, progress);
    }

    public async Task<bool> EnrichTitlesFromLocalStateAsync()
    {
        var changed = await Task.Run(EnrichTitlesFromLocalState);
        if (changed)
        {
            RefreshSessions(Store.Sessions.Values);
        }
        return changed;
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        var json = JsonSerializer.Serialize(Store, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json);
    }

    public void RefreshSessions(IEnumerable<ArchiveSession> sessions)
    {
        Sessions.Clear();
        foreach (var session in OrderedVisibleSessions(sessions).Take(600))
        {
            if (session.Tags.Count == 0)
            {
                session.Tags.Add("archive");
                if (session.CodeBlocks.Count > 0) session.Tags.Add("code");
            }
            Sessions.Add(session);
        }
    }

    public IReadOnlyList<ArchiveSession> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return OrderedVisibleSessions(Store.Sessions.Values).ToList();
        }

        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Store.Sessions.Values
            .Where(session => !session.Archived)
            .Where(session => terms.All(term => SearchText(session).Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(session => Score(session, terms))
            .ThenByDescending(session => session.Pinned)
            .ThenByDescending(session => session.UpdatedAt)
            .ToList();
    }

    public IReadOnlyList<ArchiveSearchHit> DeepSearch(string query, int limit = 80)
    {
        var visible = Store.Sessions.Values.Where(session => !session.Archived);
        if (string.IsNullOrWhiteSpace(query))
        {
            return OrderedVisibleSessions(visible)
                .Take(limit)
                .Select(session => new ArchiveSearchHit
                {
                    Session = session,
                    Message = session.Messages.LastOrDefault(),
                    SourceLabel = "recent chat",
                    Snippet = MakeSnippet(session.Messages.LastOrDefault()?.Text ?? session.Text, []),
                    Score = session.Pinned ? 5 : 0
                })
                .ToList();
        }

        var terms = QueryTerms(query);
        return visible
            .SelectMany(session => DeepHitsForSession(session, terms))
            .OrderByDescending(hit => hit.Score)
            .ThenByDescending(hit => hit.Session.Pinned)
            .ThenByDescending(hit => hit.Session.UpdatedAt)
            .Take(limit)
            .ToList();
    }

    public async Task RenameSessionAsync(ArchiveSession session, string title)
    {
        session.CustomTitle = CleanTitle(title);
        await SaveAsync();
        RefreshSessions(Store.Sessions.Values);
    }

    public async Task TogglePinAsync(ArchiveSession session)
    {
        session.Pinned = !session.Pinned;
        await SaveAsync();
        RefreshSessions(Store.Sessions.Values);
    }

    public async Task ArchiveSessionAsync(ArchiveSession session)
    {
        session.Archived = true;
        await SaveAsync();
        RefreshSessions(Store.Sessions.Values);
    }

    public async Task AddToCollectionAsync(ArchiveSession session, string collectionName)
    {
        var id = Slug(collectionName);
        if (!Store.Collections.TryGetValue(id, out var collection))
        {
            collection = new ArchiveCollection { Id = id, Name = collectionName, Color = Store.Settings.AccentHex };
            Store.Collections[id] = collection;
        }
        if (!collection.SessionIds.Contains(session.Id)) collection.SessionIds.Add(session.Id);
        await SaveAsync();
    }

    public AiProviderSettings EnsureAiProvider(string name, string baseUrl, string model)
    {
        var id = Slug(name);
        if (!Store.Settings.AiProviders.Any(provider => provider.Id == id))
        {
            Store.Settings.AiProviders.Add(new AiProviderSettings
            {
                Id = id,
                Name = name,
                BaseUrl = baseUrl,
                Model = model,
                Models = string.IsNullOrWhiteSpace(model) ? new List<string>() : new List<string> { model },
                Kind = "openai-compatible",
                Enabled = true
            });
        }
        Store.Settings.ActiveAiProviderId = id;
        return Store.Settings.AiProviders.First(provider => provider.Id == id);
    }

    public AiProviderSettings? ActiveAiProvider()
    {
        return Store.Settings.AiProviders.FirstOrDefault(provider => provider.Id == Store.Settings.ActiveAiProviderId)
               ?? Store.Settings.AiProviders.FirstOrDefault();
    }

    public IReadOnlyList<ArchiveSearchHit> RetrieveForQuestion(string question, int limit = 10)
    {
        return DeepSearch(question, limit);
    }

    public string CopyPayload(ArchiveSession session, string mode)
    {
        return mode switch
        {
            "code" => session.CodeBlocks.Count == 0
                ? "No code blocks found."
                : string.Join("\n\n", session.CodeBlocks.Select(block => $"```{block.Language}\n{block.Code}\n```")),
            "path" => session.SourcePath,
            "paths" => $"Chat source: {session.SourcePath}\nWorkspace: {session.Workspace}",
            "restore" => RestorePacket(session),
            _ => ResumePrompt(session)
        };
    }

    public string RestorePacket(ArchiveSession session)
    {
        var firstUser = session.Messages.FirstOrDefault(m => m.Role == "user")?.Text ?? session.Title;
        return string.Join("\n\n", new[]
        {
            $"# Restore Packet: {session.DisplayTitle}",
            $"## Goal summary\n{firstUser}",
            $"## Current state\nLast archived activity: {session.UpdatedAt}",
            $"## Important paths\nChat source: {session.SourcePath}\nWorkspace: {session.Workspace}",
            $"## Code blocks\n{CopyPayload(session, "code")}",
            "## Suggested first prompt\nContinue from this restore packet. Treat source paths as read-only and recover the relevant code, decisions, and next actions."
        });
    }

    public async Task<int> IndexRootAsync(string rootPath, IProgress<string>? progress = null)
    {
        rootPath = Environment.ExpandEnvironmentVariables(rootPath.Trim());
        if (!Directory.Exists(rootPath))
        {
            progress?.Report("Root does not exist.");
            return 0;
        }

        Store.Settings.ChatRootPath = rootPath;
        var files = EnumerateSessionFiles(rootPath).Take(5000).ToList();
        var indexed = 0;
        if (files.Count > 0 && HasOnlyDemoSessions())
        {
            RemoveDemoSessions();
        }

        foreach (var file in files)
        {
            try
            {
                var session = await ParseJsonlAsync(file);
                if (session.Messages.Count == 0)
                {
                    continue;
                }
                Store.Sessions[session.Id] = session;
                indexed++;
                if (indexed % 25 == 0) progress?.Report($"Indexed {indexed}/{files.Count}");
            }
            catch (Exception ex)
            {
                progress?.Report($"Skipped {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Store.Settings.LastIndexedAt = DateTime.UtcNow.ToString("O");
        Store.Settings.LastIndexStatus = indexed == 0
            ? $"No chat sessions found under {rootPath}."
            : $"Indexed {indexed} chats from {rootPath}.";
        await SaveAsync();
        RefreshSessions(Store.Sessions.Values.OrderByDescending(s => s.UpdatedAt));
        progress?.Report(Store.Settings.LastIndexStatus);
        return indexed;
    }

    private async Task<ArchiveSession> ParseJsonlAsync(string filePath)
    {
        var messages = new ObservableCollection<ArchiveMessage>();
        var codeBlocks = new ObservableCollection<CodeBlock>();
        var id = Path.GetFileNameWithoutExtension(filePath);
        var cwd = "";
        var created = "";
        var updated = "";

        foreach (var line in await File.ReadAllLinesAsync(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(created)) created = timestamp;
            updated = timestamp;

            if (root.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("id", out var idProp)) id = idProp.GetString() ?? id;
                if (payload.TryGetProperty("cwd", out var cwdProp)) cwd = cwdProp.GetString() ?? cwd;
                if (payload.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "message")
                {
                    var role = payload.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "message" : "message";
                    var text = ExtractContent(payload);
                    AddMessage(messages, codeBlocks, role, text, timestamp);
                }
            }
        }

        var title = CleanFallbackTitle(messages.FirstOrDefault(m => m.Role == "user" && IsTitleCandidate(m.Text))?.Text ?? Path.GetFileNameWithoutExtension(filePath));
        return new ArchiveSession
        {
            Id = id,
            Title = title,
            SourcePath = filePath,
            CreatedAt = created,
            UpdatedAt = updated,
            Workspace = string.IsNullOrWhiteSpace(cwd) ? "Unknown workspace" : cwd,
            WorkspaceName = string.IsNullOrWhiteSpace(cwd) ? "Unknown" : Path.GetFileName(cwd.TrimEnd('\\', '/')),
            Model = "codex",
            Messages = messages,
            CodeBlocks = codeBlocks,
            Text = string.Join("\n\n", messages.Select(m => m.Text)),
            Tags = new ObservableCollection<string>(codeBlocks.Count > 0 ? new[] { "archive", "code" } : new[] { "archive" })
        };
    }

    private static void AddMessage(ObservableCollection<ArchiveMessage> messages, ObservableCollection<CodeBlock> codeBlocks, string role, string text, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var capped = text.Length > 4000 ? text[..4000] + "\n\n[Truncated in native index.]" : text;
        var blocks = ExtractCodeBlocks(capped);
        foreach (var block in blocks) codeBlocks.Add(block);
        messages.Add(new ArchiveMessage
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Role = role,
            Text = capped,
            Timestamp = timestamp,
            CodeBlocks = new ObservableCollection<CodeBlock>(blocks)
        });
    }

    private static string ExtractContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var content)) return "";
        if (content.ValueKind == JsonValueKind.String) return content.GetString() ?? "";
        if (content.ValueKind != JsonValueKind.Array) return content.ToString();
        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var text)) builder.AppendLine(text.GetString());
            else if (item.TryGetProperty("input_text", out var input)) builder.AppendLine(input.GetString());
            else if (item.TryGetProperty("output_text", out var output)) builder.AppendLine(output.GetString());
        }
        return builder.ToString();
    }

    private static List<CodeBlock> ExtractCodeBlocks(string text)
    {
        return Regex.Matches(text, "```([a-zA-Z0-9_+.-]*)\\n([\\s\\S]*?)```")
            .Select(match => new CodeBlock
            {
                Language = string.IsNullOrWhiteSpace(match.Groups[1].Value) ? "text" : match.Groups[1].Value,
                Code = match.Groups[2].Value.Trim()
            })
            .ToList();
    }

    private static bool IsTitleCandidate(string text)
    {
        var value = text.Trim();
        return value.Length > 0 && !value.StartsWith("<environment_context>") && !value.StartsWith("<goal_context>");
    }

    private static string CleanTitle(string text)
    {
        var value = Regex.Replace(text.Trim(), "\\s+", " ");
        return value[..Math.Min(120, value.Length)];
    }

    private static string CleanFallbackTitle(string text)
    {
        var value = Regex.Replace(text.Trim(), "\\s+", " ");
        value = Regex.Replace(value, @"^#\s*Context from my IDE setup:\s*", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"^##\s*Open tabs:\s*-\s*", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"^<environment_context>.*?</environment_context>\s*", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (string.IsNullOrWhiteSpace(value)) value = "Untitled chat";
        return CleanTitle(value);
    }

    private static string SearchText(ArchiveSession session)
    {
        return $"{session.Id}\n{session.DisplayTitle}\n{session.Title}\n{session.Text}\n{session.SourcePath}\n{session.Workspace}\n{string.Join(' ', session.Tags)}";
    }

    private static IEnumerable<string> EnumerateSessionFiles(string rootPath)
    {
        try
        {
            return Directory.EnumerateFiles(rootPath, "*.jsonl", SearchOption.AllDirectories)
                .Where(file => !IsKnownNonSessionJsonl(file))
                .OrderByDescending(file => File.GetLastWriteTimeUtc(file))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool ContainsSessionJsonl(string rootPath)
    {
        return EnumerateSessionFiles(rootPath).Take(25).Any();
    }

    private static bool IsKnownNonSessionJsonl(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return name.Equals("session_index.jsonl", StringComparison.OrdinalIgnoreCase)
               || name.Equals("history.jsonl", StringComparison.OrdinalIgnoreCase)
               || name.Equals("responses.jsonl", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddIfUnique(List<string> roots, string path)
    {
        if (!roots.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
        {
            roots.Add(path);
        }
    }

    private bool HasOnlyDemoSessions()
    {
        return Store.Sessions.Count > 0 && Store.Sessions.Values.All(IsDemoSession);
    }

    private void RemoveDemoSessions()
    {
        foreach (var id in Store.Sessions.Values.Where(IsDemoSession).Select(session => session.Id).ToList())
        {
            Store.Sessions.Remove(id);
        }

        foreach (var collection in Store.Collections.Values)
        {
            collection.SessionIds.RemoveAll(id => !Store.Sessions.ContainsKey(id));
        }
    }

    private static bool IsDemoSession(ArchiveSession session)
    {
        return session.Model == "local-demo"
               || session.SourcePath.StartsWith("data/fixtures/", StringComparison.OrdinalIgnoreCase)
               || session.SourcePath.StartsWith("data\\fixtures\\", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<ArchiveSearchHit> DeepHitsForSession(ArchiveSession session, string[] terms)
    {
        var sessionScore = FuzzyScore($"{session.DisplayTitle}\n{session.WorkspaceName}\n{session.Workspace}\n{session.SourcePath}\n{string.Join(' ', session.Tags)}", terms);
        if (sessionScore > 0)
        {
            yield return new ArchiveSearchHit
            {
                Session = session,
                SourceLabel = "title/path/tags",
                Snippet = MakeSnippet($"{session.DisplayTitle}\n{session.Workspace}\n{session.SourcePath}", terms),
                MatchedTerms = MatchedTerms($"{session.DisplayTitle}\n{session.Workspace}\n{session.SourcePath}", terms),
                Score = sessionScore + 20
            };
        }

        foreach (var message in session.Messages)
        {
            var score = FuzzyScore(message.Text, terms);
            if (score <= 0) continue;
            yield return new ArchiveSearchHit
            {
                Session = session,
                Message = message,
                SourceLabel = message.RoleLabel,
                Snippet = MakeSnippet(message.Text, terms),
                MatchedTerms = MatchedTerms(message.Text, terms),
                Score = score
            };
        }

        foreach (var block in session.CodeBlocks)
        {
            var score = FuzzyScore(block.Code, terms);
            if (score <= 0) continue;
            yield return new ArchiveSearchHit
            {
                Session = session,
                SourceLabel = string.IsNullOrWhiteSpace(block.Language) ? "code" : $"code:{block.Language}",
                Snippet = MakeSnippet(block.Code, terms),
                MatchedTerms = MatchedTerms(block.Code, terms),
                Score = score + 8
            };
        }
    }

    private static string[] QueryTerms(string query)
    {
        return Regex.Matches(query.ToLowerInvariant(), "[a-z0-9_./\\\\:-]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 1)
            .Distinct()
            .Take(12)
            .ToArray();
    }

    private static int FuzzyScore(string text, string[] terms)
    {
        if (terms.Length == 0 || string.IsNullOrWhiteSpace(text)) return 0;
        var normalized = Regex.Replace(text.ToLowerInvariant(), "\\s+", " ");
        var score = 0;
        foreach (var term in terms)
        {
            if (normalized.Contains(term))
            {
                score += term.Length >= 5 ? 20 : 12;
                continue;
            }

            var compactTerm = Regex.Replace(term, "[^a-z0-9]", "");
            if (compactTerm.Length >= 4 && IsLooseSubsequence(compactTerm, normalized))
            {
                score += 7;
                continue;
            }

            if (compactTerm.Length >= 5 && HasCloseToken(compactTerm, normalized))
            {
                score += 5;
            }
        }
        return score;
    }

    private static bool IsLooseSubsequence(string needle, string haystack)
    {
        var index = 0;
        foreach (var ch in haystack)
        {
            if (index < needle.Length && ch == needle[index]) index++;
            if (index == needle.Length) return true;
        }
        return false;
    }

    private static bool HasCloseToken(string term, string text)
    {
        foreach (Match match in Regex.Matches(text, "[a-z0-9_./\\\\:-]+"))
        {
            var token = Regex.Replace(match.Value, "[^a-z0-9]", "");
            if (token.Length < 4 || Math.Abs(token.Length - term.Length) > 2) continue;
            if (LevenshteinDistanceWithin(term, token, 2)) return true;
        }
        return false;
    }

    private static bool LevenshteinDistanceWithin(string left, string right, int maxDistance)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var best = current[0];
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                best = Math.Min(best, current[j]);
            }
            if (best > maxDistance) return false;
            (previous, current) = (current, previous);
        }
        return previous[right.Length] <= maxDistance;
    }

    private static string MakeSnippet(string text, string[] terms)
    {
        var clean = Regex.Replace(text ?? "", "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(clean)) return "";

        var index = -1;
        foreach (var term in terms)
        {
            index = clean.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) break;
        }
        if (index < 0) index = 0;

        var start = Math.Max(0, index - 120);
        var length = Math.Min(clean.Length - start, 280);
        var snippet = clean.Substring(start, length);
        if (start > 0) snippet = "... " + snippet;
        if (start + length < clean.Length) snippet += " ...";
        return snippet;
    }

    private static string MatchedTerms(string text, string[] terms)
    {
        var matches = terms
            .Where(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToArray();
        return matches.Length == 0 ? "fuzzy match" : string.Join(", ", matches);
    }

    private static int Score(ArchiveSession session, string[] terms)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (session.DisplayTitle.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 8;
            if (session.CodeBlocks.Any(block => block.Code.Contains(term, StringComparison.OrdinalIgnoreCase))) score += 5;
            if (session.SourcePath.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 3;
        }
        return score;
    }

    private string ResumePrompt(ArchiveSession session)
    {
        return $"Continue this archived work from local context.\n\nChat title: {session.DisplayTitle}\nSource path: {session.SourcePath}\nWorkspace: {session.Workspace}\n\nImportant context:\n" +
               string.Join("\n", session.Messages.TakeLast(6).Select(m => $"- {m.Role}: {Regex.Replace(m.Text, "\\s+", " ")[..Math.Min(260, Regex.Replace(m.Text, "\\s+", " ").Length)]}"));
    }

    private bool NormalizeSettings()
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(Store.Settings.Theme))
        {
            Store.Settings.Theme = "amoled";
            changed = true;
        }
        var oldBlueAccent = "codex-" + "blue";
        if (Store.Settings.Accent is "mint" or "" or null || Store.Settings.Accent == oldBlueAccent)
        {
            Store.Settings.Accent = "rose";
            Store.Settings.AccentHex = "#fb7185";
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(Store.Settings.Radius))
        {
            Store.Settings.Radius = "compact";
            changed = true;
        }
        if (Store.Settings.PanelRadius == 0)
        {
            Store.Settings.PanelRadius = Store.Settings.Radius == "compact" ? 12 : Store.Settings.Radius == "rounded" ? 18 : 24;
            changed = true;
        }
        if (Store.Settings.AiProviders.Count == 0)
        {
            Store.Settings.AiProviders.Add(new AiProviderSettings
            {
                Id = "deepseek",
                Name = "DeepSeek",
                BaseUrl = "https://api.deepseek.com",
                Model = "deepseek-v4-flash",
                Models = new List<string> { "deepseek-v4-flash" },
                Kind = "openai-compatible",
                Enabled = true
            });
            Store.Settings.AiProviders.Add(new AiProviderSettings
            {
                Id = "openai",
                Name = "OpenAI",
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4.1-mini",
                Models = new List<string> { "gpt-4.1-mini" },
                Kind = "openai-compatible",
                Enabled = true
            });
            Store.Settings.ActiveAiProviderId = "deepseek";
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(Store.Settings.ActiveAiProviderId) ||
            Store.Settings.AiProviders.All(provider => provider.Id != Store.Settings.ActiveAiProviderId))
        {
            Store.Settings.ActiveAiProviderId = Store.Settings.AiProviders.First().Id;
            changed = true;
        }
        Store.Settings.ReadOnlySourceMode = true;
        return changed;
    }

    private bool EnrichTitlesFromLocalState()
    {
        var titles = LoadThreadTitles();
        if (titles.Count == 0) return false;

        var changed = false;
        foreach (var session in Store.Sessions.Values)
        {
            if (!titles.TryGetValue(session.Id, out var title) || string.IsNullOrWhiteSpace(title.Name)) continue;
            var clean = CleanTitle(title.Name);
            if (!string.Equals(session.Title, clean, StringComparison.Ordinal))
            {
                session.Title = clean;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(title.UpdatedAt) && string.CompareOrdinal(title.UpdatedAt, session.UpdatedAt) > 0)
            {
                session.UpdatedAt = title.UpdatedAt;
                changed = true;
            }
        }
        return changed;
    }

    private Dictionary<string, ThreadTitle> LoadThreadTitles()
    {
        var result = new Dictionary<string, ThreadTitle>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in CandidateSessionIndexFiles())
        {
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("id", out var idProp)) continue;
                    var id = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var name = root.TryGetProperty("thread_name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    var updated = root.TryGetProperty("updated_at", out var updatedProp) ? updatedProp.GetString() ?? "" : "";
                    PutTitle(result, id, name, updated);
                }
            }
            catch
            {
                // Damaged index files are expected after repair experiments; ignore and continue.
            }
        }

        LoadSqliteThreadTitles(result);
        return result;
    }

    private static IEnumerable<string> CandidateSessionIndexFiles()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, ".codex");
        var live = Path.Combine(root, "session_index.jsonl");
        if (File.Exists(live)) yield return live;
        var backups = Path.Combine(root, "repair-backups");
        if (!Directory.Exists(backups)) yield break;
        foreach (var file in Directory.EnumerateFiles(backups, "session_index.jsonl", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static void LoadSqliteThreadTitles(Dictionary<string, ThreadTitle> result)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "state_5.sqlite");
        if (!File.Exists(path)) return;

        try
        {
            var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select id, title, updated_at_ms, updated_at from threads";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var name = reader.GetString(1);
                var updatedMs = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                var updatedSeconds = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                var updated = updatedMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(updatedMs).UtcDateTime.ToString("O")
                    : updatedSeconds > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(updatedSeconds).UtcDateTime.ToString("O")
                        : "";
                PutTitle(result, id, name, updated);
            }
        }
        catch
        {
            // SQLite may be locked or absent on older installs. JSONL titles remain enough for those cases.
        }
    }

    private static void PutTitle(Dictionary<string, ThreadTitle> result, string id, string name, string updatedAt)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return;
        if (!result.TryGetValue(id, out var existing) || string.CompareOrdinal(updatedAt, existing.UpdatedAt) >= 0)
        {
            result[id] = new ThreadTitle(name, updatedAt);
        }
    }

    private static IEnumerable<ArchiveSession> OrderedVisibleSessions(IEnumerable<ArchiveSession> sessions)
    {
        return sessions
            .Where(session => !session.Archived)
            .OrderByDescending(session => session.Pinned)
            .ThenByDescending(session => session.UpdatedAt);
    }

    private static string Slug(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "collection" : slug;
    }

    private sealed record ThreadTitle(string Name, string UpdatedAt);

    private static string DefaultStorePath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "CodexLocalRetrieval", "app-store.json");
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "data", "app-store.json")) || Directory.Exists(Path.Combine(dir, "data", "fixtures")))
            {
                return dir;
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent ?? "";
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
