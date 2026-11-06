using System.Reflection;
using Akka.Util;
using Ohmline.ActorShell.Clustering;

namespace Ohmline.ActorShell.Tests.Clustering;

/// <summary>
/// The app version a node gives the cluster (section 21.2), and the two traps on the way to it:
/// MinVer's assembly version, which names only the major version, and Akka.NET's parser, which
/// refuses a dotted pre-release.
/// </summary>
public sealed class AppVersionTests
{
    private static readonly Assembly Built = typeof(AppVersionTests).Assembly;

    [Fact]
    public void The_build_writes_the_release_s_major_minor_patch_and_akka_parses_it()
    {
        var appVersion = OhmlineAppVersion.Of(Built);

        appVersion.ShouldBe(ReleaseOf(Built));
        AppVersion.Create(appVersion).ToString().ShouldBe(appVersion);
    }

    [Fact]
    public void MinVer_s_assembly_version_names_only_the_major_version()
    {
        var assemblyVersion = Built.GetName().Version!;

        (assemblyVersion.Minor, assemblyVersion.Build, assemblyVersion.Revision)
            .ShouldBe((0, 0, 0));
    }

    [Theory]
    [InlineData("1.4.3-alpha.0.1")]
    [InlineData("2.0.0-rc.1")]
    public void Given_a_dotted_pre_release_then_akka_cannot_parse_it(string version)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => AppVersion.Create(version));
    }

    [Theory]
    [InlineData("1.4.3-alpha.0.1")]
    [InlineData("1.4.3-rc1")]
    [InlineData("1.4.3+7e4f0285")]
    [InlineData("1.4")]
    [InlineData("1.4.3.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Given_anything_but_major_minor_patch_then_it_never_reaches_akka(string? version)
    {
        OhmlineAppVersion.IsMajorMinorPatch(version).ShouldBeFalse();
    }

    [Fact]
    public void Given_an_assembly_without_an_app_version_then_it_is_refused_by_name()
    {
        var unversioned = typeof(object).Assembly;

        Should.Throw<InvalidOperationException>(() => OhmlineAppVersion.Of(unversioned))
            .Message.ShouldContain("not MAJOR.MINOR.PATCH");
    }

    [Fact]
    public void Akka_orders_app_versions_by_number_not_by_text()
    {
        AppVersion.Create("1.10.0").ShouldBeGreaterThan(AppVersion.Create("1.4.3"));
        AppVersion.Create("1.4.3").ShouldBeGreaterThan(AppVersion.Create("1.4.3-rc1"));
    }

    /// <summary>MinVer's version (<c>1.4.3-alpha.0.1+7e4f0285</c>) up to its first dash.</summary>
    private static string ReleaseOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('-', '+')[0];
}
