using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ohmline.Testing;

/// <summary>
/// Golden files: the exact JSON a message was written as, committed under <c>tests/golden/</c>
/// so that a change to a persisted or wire shape shows up in review instead of in production. A
/// test compares what it writes now with the file and fails on any difference. To accept a new
/// or deliberately changed shape, run the tests once with <c>OHMLINE_ACCEPT_GOLDEN=1</c>, read
/// the diff, and commit it.
/// </summary>
public static class Golden
{
    public const string AcceptVariable = "OHMLINE_ACCEPT_GOLDEN";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
    };

    /// <summary>The folder that holds the golden files: <c>tests/golden</c>.</summary>
    public static DirectoryInfo Folder { get; } = FindFolder();

    private static bool Accepting =>
        Environment.GetEnvironmentVariable(AcceptVariable) == "1";

    /// <summary>
    /// Checks <paramref name="json"/> against <c>tests/golden/{name}.json</c>, or writes it there
    /// when accepting. Only the layout is normalized (indented, one member per line); every name,
    /// value and order is compared as written.
    /// </summary>
    public static void Verify(string name, string json)
    {
        var path = PathOf(name);
        var actual = Normalize(json);
        if (Accepting)
        {
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).ShouldBeTrue(
            $"There is no golden file {path}. If the JSON below is right, run the tests with " +
            $"{AcceptVariable}=1, review the new file and commit it.\n{actual}");
        actual.ShouldBe(
            File.ReadAllText(path).ReplaceLineEndings("\n"),
            $"{name} no longer serializes as its golden file says. A shape that has been " +
            "written must stay readable: add a new manifest version instead of changing this " +
            $"one. Accept the change with {AcceptVariable}=1 only if nothing was ever written.");
    }

    /// <summary>The golden JSON for <paramref name="name"/>, to show it still reads.</summary>
    public static string Read(string name) => File.ReadAllText(PathOf(name));

    private static string PathOf(string name) => Path.Combine(Folder.FullName, $"{name}.json");

    private static string Normalize(string json) =>
        JsonNode.Parse(json)!.ToJsonString(Indented) + "\n";

    private static DirectoryInfo FindFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
             folder is not null;
             folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Ohmline.slnx")))
            {
                return new DirectoryInfo(Path.Combine(folder.FullName, "tests", "golden"));
            }
        }

        throw new InvalidOperationException(
            $"No Ohmline.slnx above {AppContext.BaseDirectory}: cannot find tests/golden.");
    }
}
