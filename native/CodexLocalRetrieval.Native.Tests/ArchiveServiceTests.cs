using CodexLocalRetrieval.Core.Services;

namespace CodexLocalRetrieval.Native.Tests;

[TestClass]
public sealed class ArchiveServiceTests
{
    [TestMethod]
    public async Task LoadAsync_UsesRoseAsNativeDefault()
    {
        var service = CreateService();

        await service.LoadAsync();

        Assert.AreNotEqual("codex-blue", service.Store.Settings.Accent);
        Assert.StartsWith("#", service.Store.Settings.AccentHex);
        Assert.AreEqual("compact", service.Store.Settings.Radius);
        Assert.IsTrue(service.Store.Settings.ReadOnlySourceMode);
        Assert.IsTrue(service.Store.Settings.AiProviders.Any(provider => provider.Id == "deepseek"));
        Assert.AreEqual("deepseek", service.ActiveAiProvider()?.Id);
        Assert.AreNotEqual(0, service.Sessions.Count);
    }

    [TestMethod]
    public async Task Search_FindsFixtureBySessionId()
    {
        var service = CreateService();
        await service.LoadAsync();

        var results = service.Search("fixture-b");

        Assert.IsTrue(results.Any(session => session.Id == "fixture-b"));
    }

    [TestMethod]
    public async Task CopyPayload_BuildsRestorePacketAndCodePayload()
    {
        var service = CreateService();
        await service.LoadAsync();
        var fixture = service.Search("fixture-b").First();

        var restore = service.CopyPayload(fixture, "restore");
        var code = service.CopyPayload(fixture, "code");

        StringAssert.Contains(restore, "Restore Packet");
        StringAssert.Contains(code, "export function score");
    }

    [TestMethod]
    public async Task DeepSearch_ReturnsContentSnippetsAndPathPayload()
    {
        var service = CreateService();
        await service.LoadAsync();

        var hits = service.DeepSearch("score export");
        var fixture = service.Search("fixture-b").First();
        var path = service.CopyPayload(fixture, "path");

        Assert.IsTrue(hits.Any(hit => hit.Session.Id == "fixture-b" && hit.Snippet.Contains("score")));
        Assert.AreEqual(fixture.SourcePath, path);
    }

    private static ArchiveService CreateService()
    {
        var tempStore = Path.Combine(Path.GetTempPath(), "CodexLocalRetrieval.Tests", Guid.NewGuid().ToString("N"), "app-store.json");
        return new ArchiveService(tempStore, FindRepositoryRoot());
    }

    private static string FindRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "CodexLocalRetrieval.sln")))
            {
                return dir;
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent ?? "";
        }
        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
