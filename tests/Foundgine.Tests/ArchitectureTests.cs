using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Foundgine.Tests;

/// <summary>
/// Turns the dependency-direction rules described in the root README's diagrams
/// into an executable check, instead of a claim that only holds until someone
/// adds a ProjectReference without reading the README first.
///
/// This parses each src/*/*.csproj's &lt;ProjectReference&gt; elements directly
/// rather than loading the built assemblies and inspecting
/// Assembly.GetReferencedAssemblies(). That was the first approach tried here,
/// and it's the wrong one: Roslyn only emits AssemblyRef metadata for a
/// referenced assembly whose types are actually used, so an unused
/// ProjectReference — exactly the kind of latent, accidental violation this
/// test exists to catch — would be invisible to it. Reading the .csproj graph
/// directly also means this test needs no build step to run against source.
/// </summary>
public class ArchitectureTests
{
    // Resolved from this file's own path (via CallerFilePath) rather than the
    // test runner's working directory, which varies by IDE/CLI/CI runner.
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot([CallerFilePath] string here = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Foundgine.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate Foundgine.sln by walking up from this test file's path.");
    }

    /// <summary>
    /// The only edges the architecture allows, expressed as
    /// (project -> projects it may take a ProjectReference on). This is the
    /// machine-checked version of the dependency diagram in README.md — keep
    /// the two in sync. Graphgine and everything downstream of it moved to
    /// archive/ as part of the minimal-active-tree restructure and is no
    /// longer part of the active src/ tree this test scans, so it no longer
    /// needs an entry here. Foundgine.Reflection and Foundgine.Serialization
    /// moved to archive/ for the same reason.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["Foundgine.Abstractions"] = [],
        ["Foundgine.Foundation"] = ["Foundgine.Abstractions"],
        ["Foundgine.Metadata"] = ["Foundgine.Foundation"],
        ["Foundgine.Diagnostics"] = ["Foundgine.Foundation"],
        ["Foundgine.Builders"] = ["Foundgine.Metadata"],
        ["Foundgine.Execution.Contracts"] = ["Foundgine.Metadata"],
        ["Foundgine.Planning"] = ["Foundgine.Metadata", "Foundgine.Builders"],
        ["Foundgine.Providers"] = ["Foundgine.Execution.Contracts", "Foundgine.Builders"],
    };

    private static IEnumerable<(string Project, string CsprojPath)> DiscoverProjects() =>
        Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => (Project: Path.GetFileNameWithoutExtension(path), CsprojPath: path));

    // csproj files in this repo use Windows-style backslash paths in
    // <ProjectReference Include="..\Foo\Foo.csproj" />. Path.GetFileNameWithoutExtension
    // only recognizes '/' as a separator on Linux, so normalize first — otherwise
    // this "passes" on Windows and silently checks nothing on Linux CI.
    private static string ProjectNameFromInclude(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace('\\', '/').Split('/').Last());

    private static string[] ReferencesOf(string csprojPath) =>
        XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => ProjectNameFromInclude((string)e.Attribute("Include")!))
            .ToArray();

    public static IEnumerable<object[]> Projects() =>
        DiscoverProjects().Select(p => new object[] { p.Project, p.CsprojPath });

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_only_references_allowed_projects(string project, string csprojPath)
    {
        if (!AllowedReferences.TryGetValue(project, out var allowed))
            return;

        var disallowed = ReferencesOf(csprojPath).Except(allowed).ToArray();

        Assert.True(
            disallowed.Length == 0,
            $"{project} has a ProjectReference on [{string.Join(", ", disallowed)}], which " +
            $"{(disallowed.Length == 1 ? "isn't an allowed dependency" : "aren't allowed dependencies")} " +
            $"per the architecture. Allowed for {project}: [{string.Join(", ", allowed)}]. " +
            "If this is an intentional architecture change, update AllowedReferences here " +
            "*and* the dependency diagram in README.md together.");
    }

    // The Graphgine-specific "never references generated metadata symbols"
    // check that used to live here was removed along with src/Graphgine when
    // Graphgine moved to archive/ during the minimal-active-tree restructure
    // -- it asserted Directory.Exists(src/Graphgine), which no longer holds.
    // Restore an equivalent check (against archive/src/Graphgine, or against
    // whatever GraphQL-facing project comes back out of the archive) if that
    // layer becomes active again.

    [Fact]
    public void No_project_transitively_depends_on_itself()
    {
        var graph = DiscoverProjects().ToDictionary(p => p.Project, p => ReferencesOf(p.CsprojPath));

        foreach (var start in graph.Keys)
        {
            var seen = new HashSet<string> { start };
            var stack = new Stack<string>(graph.TryGetValue(start, out var direct) ? direct : []);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                Assert.True(current != start, $"Cycle detected: {start} transitively depends on itself.");

                if (!seen.Add(current) || !graph.TryGetValue(current, out var refs))
                    continue;

                foreach (var r in refs)
                    stack.Push(r);
            }
        }
    }
}
