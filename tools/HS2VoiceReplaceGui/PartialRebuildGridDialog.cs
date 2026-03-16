using System.ComponentModel;
using System.Media;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

// Owns the partial rebuild dialog shell and shared state used across its layout, data, and action partials.

internal sealed partial class PartialRebuildGridDialog : Form
{
    private static readonly Regex FullRunProgressRegex = new(@"\[(?<done>\d+)\s*/\s*(?<total>\d+)\]", RegexOptions.Compiled);
    private static readonly Regex ProgressRelRegex = new(@"rel=(?<rel>\S+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ProgressStatusRegex = new(@"\]\s*(?<status>[a-zA-Z_]+)\s+exit=", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ProgressIndexRegex = new(@"\[(?<done>\d+)\s*/\s*(?<total>\d+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly TextBox _txtRunRoot = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly Button _btnReload = new() { Text = UiTextCatalog.Get("button.reload"), Width = 110, Height = 36 };
    private readonly Button _btnRunFull = new() { Text = UiTextCatalog.Get("button.runAll"), Width = 140, Height = 36 };
    private readonly Button _btnStopFull = new() { Text = UiTextCatalog.Get("button.stop"), Width = 110, Height = 36, Enabled = false };
    private readonly Button _btnPlaySelectedSrc = new() { Text = UiTextCatalog.Get("button.playSrc"), Width = 130, Height = 36, Enabled = false };
    private readonly Button _btnPlaySelectedDst = new() { Text = UiTextCatalog.Get("button.playDst"), Width = 150, Height = 36, Enabled = false };
    private readonly Button _btnRebuildSelected = new() { Text = UiTextCatalog.Get("button.rebuildRow"), Width = 160, Height = 36, Enabled = false };
    private readonly Button _btnDiscardSelected = new() { Text = UiTextCatalog.Get("button.discardDst"), Width = 150, Height = 36, Enabled = false };
    private readonly Panel _gridPanel = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false };
    private readonly Label _lblEmptyState = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        AutoSize = false,
        Visible = false,
    };
    private readonly ProgressBar _pbFull = new() { Dock = DockStyle.Fill, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
    private readonly Label _lblStatus = new() { AutoSize = true };
    private readonly BindingList<PartialRebuildGridRow> _rows = new();
    private readonly Dictionary<string, PartialRebuildGridRow> _rowByRel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _rowIndexByRel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _relByProgressIndex = new();
    private readonly Action<string> _onLog;
    private readonly Func<PartialRebuildGridRow, Task<string>> _onRebuild;
    private readonly Func<Action<string>, Task<string>> _onRunFull;
    private readonly Func<bool> _canRunFull;
    private readonly Func<bool> _isOwnerBusy;
    private readonly Func<bool> _onCancelFull;
    private readonly bool _useEnglish;
    private bool _busy;
    private bool _fullRunExecuting;
    private SoundPlayer? _activePlayer;

    private UiLanguage Language => _useEnglish ? UiLanguage.En : UiLanguage.Ja;
    private string T(string key, params object[] args) => UiTextCatalog.Get(Language, key, args);

    public string RunRoot => _txtRunRoot.Text.Trim();
}


