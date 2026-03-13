using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class LocalizedAttributesTests
{
    [Fact]
    public void LocalizedDisplayName_TracksCurrentLanguage()
    {
        var previous = LocalizationState.CurrentLanguage;
        try
        {
            var attr = new LocalizedDisplayNameAttribute("button.deploy");

            LocalizationState.CurrentLanguage = UiLanguage.Ja;
            Assert.Equal("配備", attr.DisplayName);

            LocalizationState.CurrentLanguage = UiLanguage.En;
            Assert.Equal("Deploy", attr.DisplayName);
        }
        finally
        {
            LocalizationState.CurrentLanguage = previous;
        }
    }

    [Fact]
    public void LocalizedDescription_TracksCurrentLanguage()
    {
        var previous = LocalizationState.CurrentLanguage;
        try
        {
            var attr = new LocalizedDescriptionAttribute("seedvc.engine.description");

            LocalizationState.CurrentLanguage = UiLanguage.En;
            Assert.Contains("v1", attr.Description);
            Assert.Contains("v2", attr.Description);

            LocalizationState.CurrentLanguage = UiLanguage.Ja;
            Assert.Contains("v1", attr.Description);
            Assert.Contains("v2", attr.Description);
        }
        finally
        {
            LocalizationState.CurrentLanguage = previous;
        }
    }
}

