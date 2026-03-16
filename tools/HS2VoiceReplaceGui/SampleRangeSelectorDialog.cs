using System.Media;

namespace HS2VoiceReplace;

// Hosts the range-selection dialog used to cut manual normal/ero style segments from source audio.

internal sealed partial class SampleRangeSelectorDialog : Form
{
    private string T(string key, params object[] args) => UiTextCatalog.Get(key, args);
    private readonly string _sourceFile;
    private readonly string? _ffmpegExe;
    private readonly PictureBox _waveBox = new() { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private readonly TrackBar _startTrack = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None };
    private readonly NumericUpDown _numStart = new() { DecimalPlaces = 2, Increment = 0.05M, Minimum = 0, Maximum = 99999, Width = 120 };
    private readonly NumericUpDown _numDuration = new() { DecimalPlaces = 2, Increment = 0.10M, Minimum = 0.50M, Maximum = 60.00M, Value = 10.00M, Width = 120 };
    private readonly Label _lblRange = new() { AutoSize = true };
    private readonly Label _lblInfo = new() { AutoSize = false, Dock = DockStyle.Fill, Height = 48, AutoEllipsis = true };
    private readonly Label _lblLoading = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _btnPlaySelection = new() { Text = UiTextCatalog.Get("button.play"), Width = 150, Height = 36 };
    private readonly Button _btnStopPlayback = new() { Text = UiTextCatalog.Get("button.stop"), Width = 110, Height = 36, Enabled = false };

    private float[] _envelope = Array.Empty<float>();
    private double _totalSec;
    private bool _syncing;
    private string? _analysisWavTemp;
    private string? _previewClipTemp;
    private SoundPlayer? _previewPlayer;
    private bool _isPlaying;
    private bool _disposed;
    private readonly StyleSegmentSelection? _initialSelection;
    private bool _waveReady;

    public StyleSegmentSelection? Selection { get; private set; }
}


