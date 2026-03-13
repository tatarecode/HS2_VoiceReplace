namespace HS2VoiceReplace;

// Collects UI-facing model types shared across dialogs, grids, persisted settings, and sample-asset views.

internal static class SampleAssetConstants
{
    public const string HashAlgorithmVersion = "sha256-file-v1";
}

internal sealed class BootstrapUiSettings
{
    public string? OutputRoot { get; set; }
}

internal sealed class PersistedUiSettings
{
    public string? ExternalToolsRoot { get; set; }
    public string? SourceHs2Root { get; set; }
    public string? DeployHs2Root { get; set; }
    public int? TargetPersonalityId { get; set; }
    public string? NormalSample { get; set; }
    public string? EroSample { get; set; }
    public bool? DeployToBackup { get; set; }
    public bool? SkipCompleted { get; set; }
    public SeedVcUiSettings? SeedVc { get; set; }
    public StyleSegmentSelection? ManualNormalSegment { get; set; }
    public StyleSegmentSelection? ManualEroSegment { get; set; }
    public string? LastGridRunRoot { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public string? UiLanguageCode { get; set; }
    public string? NormalSampleAssetId { get; set; }
    public string? EroSampleAssetId { get; set; }
}

internal sealed class PersonalityChoiceItem
{
    public int Id { get; init; }
    public string NameKey { get; init; } = "";
    public override string ToString() => $"c{Id:00} - {UiTextCatalog.Get(NameKey)}";
}

internal sealed class SampleAssetItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? SourceFilePath { get; set; }
    public double? SourceStartSec { get; set; }
    public double? SourceDurationSec { get; set; }
    public string RelativeWavPath { get; set; } = "";
    public string Signature { get; set; } = "";
    public string HashAlgorithmVersion { get; set; } = SampleAssetConstants.HashAlgorithmVersion;
    public double DurationSec { get; set; }
    public int SampleRateHz { get; set; }
    public int Channels { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExtractedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

internal sealed class SampleAssetGridRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public string Signature { get; set; } = "";
    public string HashAlgorithmVersion { get; set; } = "";
    public string LengthSec { get; set; } = "";
    public string SampleRateHz { get; set; } = "";
    public string Channels { get; set; } = "";
    public string Source { get; set; } = "";
    public string Range { get; set; } = "";
    public string ExtractedAt { get; set; } = "";
    public string LastUsedAt { get; set; } = "";
}

internal sealed class ComboBoxItem
{
    public ComboBoxItem(string value, string text)
    {
        Value = value;
        Text = text;
    }

    public string Value { get; }
    public string Text { get; }
    public override string ToString() => Text;
}

internal sealed class SampleAssetsCatalog
{
    public int Version { get; set; } = 1;
    public string HashAlgorithmVersion { get; set; } = SampleAssetConstants.HashAlgorithmVersion;
    public string? NormalSampleAssetId { get; set; }
    public string? EroSampleAssetId { get; set; }
    public List<SampleAssetItem> Items { get; set; } = new();
}


