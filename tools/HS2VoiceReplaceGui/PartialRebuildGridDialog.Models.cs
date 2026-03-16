namespace HS2VoiceReplace;

// Defines row/view models and small data records used by the partial rebuild dialog.

internal sealed class PartialRebuildGridRow
{
    public string RunRoot { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Bucket { get; set; } = "normal";
    public string SourceFile { get; set; } = "";
    public string ConvertedFile { get; set; } = "";
    public bool SourceExists { get; set; }
    public bool ConvertedExists { get; set; }
    public string SourceState => SourceExists ? "OK" : "NG";
    public string ConvertedState => ConvertedExists ? "OK" : "NG";
    public string Status { get; set; } = "";
    public string VoiceLine { get; set; } = "";
    public string SampleNameUsed { get; set; } = "";
    public string SampleNameNormal { get; set; } = "";
    public string SampleNameEro { get; set; } = "";
    public string SampleSignatureNormal { get; set; } = "";
    public string SampleSignatureEro { get; set; } = "";
    public string SampleSignatureUsed { get; set; } = "";
    public string SeedVcSummary { get; set; } = "";
    public string SeedVcSummaryStored { get; set; } = "";
}

internal sealed partial class PartialRebuildGridDialog
{
    private readonly record struct RunSampleSignatures(
        string Normal,
        string Ero,
        string NormalName = "",
        string EroName = "",
        string SeedVcSummary = "");
    private readonly record struct RowSampleSignature(
        string Normal,
        string Ero,
        string Used,
        string NormalName = "",
        string EroName = "",
        string UsedName = "",
        string SeedVcSummary = "");
}


