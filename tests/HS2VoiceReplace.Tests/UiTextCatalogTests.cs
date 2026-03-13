using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class UiTextCatalogTests
{
    [Fact]
    public void Get_ReturnsJapaneseText_ForJapaneseLanguage()
    {
        var text = UiTextCatalog.Get(UiLanguage.Ja, "button.deploy");
        Assert.Equal("配備", text);
    }

    [Fact]
    public void Get_ReturnsEnglishText_ForEnglishLanguage()
    {
        var text = UiTextCatalog.Get(UiLanguage.En, "button.deploy");
        Assert.Equal("Deploy", text);
    }

    [Fact]
    public void Get_FormatsArguments_UsingRequestedLanguage()
    {
        var text = UiTextCatalog.Get(UiLanguage.En, "message.notFound", "foo.txt");
        Assert.Equal("foo.txt was not found.", text);
    }

    [Fact]
    public void Get_ReturnsKeyMarker_ForMissingEntry()
    {
        var text = UiTextCatalog.Get(UiLanguage.En, "missing.key");
        Assert.Equal("[[missing.key]]", text);
    }
}

