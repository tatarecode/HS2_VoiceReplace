using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class PartialGridColumnWidthPersistenceTests
{
    [Fact]
    public void PersistedUiSettings_CanStorePartialGridColumnWidths()
    {
        var settings = new PersistedUiSettings
        {
            PartialGridColumnWidths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["RelativePath"] = 320,
                ["VoiceLine"] = 480,
            }
        };

        Assert.Equal(320, settings.PartialGridColumnWidths!["RelativePath"]);
        Assert.Equal(480, settings.PartialGridColumnWidths["VoiceLine"]);
    }
}
