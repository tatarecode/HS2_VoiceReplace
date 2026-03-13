namespace HS2VoiceReplace;

// Represents a manually selected style-audio segment including source path and time range.

internal sealed class StyleSegmentSelection
{
    public string SourceFile { get; init; } = "";
    public double StartSec { get; init; }
    public double DurationSec { get; init; }

    public double EndSec => StartSec + DurationSec;

    public StyleSegmentSelection Clone() => new()
    {
        SourceFile = SourceFile,
        StartSec = StartSec,
        DurationSec = DurationSec,
    };

    public string ToShortString()
    {
        return $"{FormatSec(StartSec)} - {FormatSec(EndSec)} ({DurationSec:F2}s)";
    }

    private static string FormatSec(double sec)
    {
        if (sec < 0) sec = 0;
        var t = TimeSpan.FromSeconds(sec);
        return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";
    }
}


