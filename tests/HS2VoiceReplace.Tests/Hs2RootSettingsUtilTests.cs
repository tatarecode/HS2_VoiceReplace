using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class Hs2RootSettingsUtilTests
{
    [Fact]
    public void ResolvePersistedHs2Root_PrefersUnifiedHs2Root()
    {
        var settings = new PersistedUiSettings
        {
            Hs2Root = @" C:\HS2\Unified ",
            SourceHs2Root = @"C:\HS2\Source",
            DeployHs2Root = @"C:\HS2\Deploy",
        };

        var actual = Hs2RootSettingsUtil.ResolvePersistedHs2Root(settings);

        Assert.Equal(@"C:\HS2\Unified", actual);
    }

    [Fact]
    public void ResolvePersistedHs2Root_FallsBackToLegacySourceRoot()
    {
        var settings = new PersistedUiSettings
        {
            SourceHs2Root = @" C:\HS2\Source ",
            DeployHs2Root = @"C:\HS2\Deploy",
        };

        var actual = Hs2RootSettingsUtil.ResolvePersistedHs2Root(settings);

        Assert.Equal(@"C:\HS2\Source", actual);
    }

    [Fact]
    public void ResolvePersistedHs2Root_FallsBackToLegacyDeployRoot()
    {
        var settings = new PersistedUiSettings
        {
            DeployHs2Root = @" C:\HS2\Deploy ",
        };

        var actual = Hs2RootSettingsUtil.ResolvePersistedHs2Root(settings);

        Assert.Equal(@"C:\HS2\Deploy", actual);
    }

    [Fact]
    public void ResolvePersistedHs2Root_ReturnsEmpty_WhenNoValueExists()
    {
        var actual = Hs2RootSettingsUtil.ResolvePersistedHs2Root(new PersistedUiSettings());

        Assert.Equal(string.Empty, actual);
    }

    [Fact]
    public void FirstNonEmpty_ReturnsFirstTrimmedValue()
    {
        var actual = Hs2RootSettingsUtil.FirstNonEmpty(" ", "  abc  ", "def");

        Assert.Equal("abc", actual);
    }
}
