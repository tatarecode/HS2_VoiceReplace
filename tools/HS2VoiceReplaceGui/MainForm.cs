using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Media;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

// Hosts the core WinForms state for the application and acts as the composition root for UI partials.

public sealed partial class MainForm : Form
{
    private readonly TextBox _txtBundleRoot = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly TextBox _txtExternalToolsRoot = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtOutputRoot = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtSourceHs2Root = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDeployRoot = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _cmbPersonality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly TextBox _txtNormalSample = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtEroSample = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtNormalSegment = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly TextBox _txtEroSegment = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly Button _btnEditNormalSegment = new() { Text = UiTextCatalog.Get("button.range"), Width = 110, Height = 34 };
    private readonly Button _btnClearNormalSegment = new() { Text = UiTextCatalog.Get("button.clear"), Width = 110, Height = 34 };
    private readonly Button _btnEditEroSegment = new() { Text = UiTextCatalog.Get("button.range"), Width = 110, Height = 34 };
    private readonly Button _btnClearEroSegment = new() { Text = UiTextCatalog.Get("button.clear"), Width = 110, Height = 34 };
    private readonly CheckBox _chkSkipCompleted = new() { Text = UiTextCatalog.Get("checkbox.skipCompleted"), Checked = true, AutoSize = true };
    private readonly Button _btnSetup = new() { Text = UiTextCatalog.Get("button.setup"), Width = 160, Height = 38 };
    private readonly Button _btnExtract = new() { Text = UiTextCatalog.Get("button.extract"), Width = 160, Height = 38 };
    private readonly Button _btnDeploy = new() { Text = UiTextCatalog.Get("button.deploy"), Width = 160, Height = 38 };
    private readonly Button _btnUndeploy = new() { Text = UiTextCatalog.Get("button.undeploy"), Width = 170, Height = 38 };
    private readonly Button _btnPreview = new() { Text = UiTextCatalog.Get("button.preview"), Width = 160, Height = 38 };
    private readonly Button _btnCancel = new() { Text = UiTextCatalog.Get("button.stop"), Width = 160, Height = 38, Enabled = false };
    private readonly Button _btnSeedVcSettings = new() { Text = UiTextCatalog.Get("button.conversionSettings"), Width = 160, Height = 38 };
    private readonly Label _lblSeedVcSummary = new() { AutoSize = true };
    private readonly Label _lblSampleSignature = new() { AutoSize = true };
    private Label? _lblSampleSignatureInDialog;
    private readonly TextBox _txtPreviewNormal = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly TextBox _txtPreviewEro = new() { Dock = DockStyle.Fill, ReadOnly = true, TabStop = false };
    private readonly Button _btnPlayPreviewNormal = new() { Text = UiTextCatalog.Get("button.play"), Width = 110, Height = 36, Enabled = false };
    private readonly Button _btnPlayPreviewEro = new() { Text = UiTextCatalog.Get("button.play"), Width = 110, Height = 36, Enabled = false };
    private Button? _btnSampleDialogCancel;
    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        ReadOnly = true,
        WordWrap = true,
        Font = new Font("Consolas", 9F),
    };
    private readonly MenuStrip _menu = new() { Dock = DockStyle.Top };
    private readonly Panel _headerHost = new() { Dock = DockStyle.Top, AutoScroll = true, Height = 170 };
    private readonly Panel _gridHost = new() { Dock = DockStyle.Fill };
    private PartialRebuildGridDialog? _embeddedGrid;
    private Form? _basicSettingsDialog;
    private Form? _sampleAudioDialog;

    private CancellationTokenSource? _cts;
    private readonly string _bundleRootFixed;
    private readonly string _defaultOutputRoot;
    private string _activeOutputRoot;
    private string UiSettingsPath => Path.Combine(_activeOutputRoot, "ui_settings.json");
    private string SampleAssetsCatalogPath => Path.Combine(_activeOutputRoot, "sample_assets.json");
    private string SampleAssetsRoot => Path.Combine(_activeOutputRoot, "sample_assets");
    private string SampleAssetsActiveRoot => Path.Combine(SampleAssetsRoot, "active");
    private string SampleAssetsTrashRoot => Path.Combine(SampleAssetsRoot, "trash");
    private string BootstrapSettingsPath => Path.Combine(AppContext.BaseDirectory, "HS2VoiceReplaceGui.bootstrap.json");
    private SplitContainer? _mainSplit;
    private UiLanguage _uiLanguage = UiLanguage.Ja;
    private SeedVcUiSettings _seedVc = SeedVcUiSettings.CreateDefault();
    private StyleSegmentSelection? _manualNormalSegment;
    private StyleSegmentSelection? _manualEroSegment;
    private string _lastPreviewNormalPath = string.Empty;
    private string _lastPreviewEroPath = string.Empty;
    private string _lastGridRunRoot = string.Empty;
    private readonly List<SampleAssetItem> _sampleAssets = new();
    private readonly IVoiceReplaceApplicationService _appService = new VoiceReplaceApplicationService();
    private string _normalSampleAssetId = string.Empty;
    private string _eroSampleAssetId = string.Empty;
    private const string SampleHashAlgorithmVersion = SampleAssetConstants.HashAlgorithmVersion;
    private const string RuntimePluginFileName = "HS2_VoiceReplace.dll";
    private const string LegacyRuntimePluginFileName = "HS2VoiceReplace.Runtime.dll";
    private static readonly Regex AutoResumeRunRootRegex = new(
        @"[\\/]+gui_runs[\\/]+resume_c\d{2}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly PersonalityChoiceItem[] PersonalityChoices =
    {
        new() { Id = 0, NameKey = "personality.00" },
        new() { Id = 1, NameKey = "personality.01" },
        new() { Id = 2, NameKey = "personality.02" },
        new() { Id = 3, NameKey = "personality.03" },
        new() { Id = 4, NameKey = "personality.04" },
        new() { Id = 5, NameKey = "personality.05" },
        new() { Id = 6, NameKey = "personality.06" },
        new() { Id = 7, NameKey = "personality.07" },
        new() { Id = 8, NameKey = "personality.08" },
        new() { Id = 9, NameKey = "personality.09" },
        new() { Id = 10, NameKey = "personality.10" },
        new() { Id = 11, NameKey = "personality.11" },
        new() { Id = 12, NameKey = "personality.12" },
        new() { Id = 13, NameKey = "personality.13" },
    };

    public MainForm()
    {
        _bundleRootFixed = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "runtime_bundle"));
        _defaultOutputRoot = BuildDefaultOutputRoot(AppContext.BaseDirectory);
        _activeOutputRoot = _defaultOutputRoot;
        LocalizationState.CurrentLanguage = _uiLanguage;
        Text = UiTextCatalog.Get(_uiLanguage, "app.title");
        Width = 1360;
        Height = 1040;
        MinimumSize = new Size(1040, 760);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ApplyLanguageToSharedControls();

        BuildLayout();
        SetDefaults();
        LoadBootstrapSettings();
        LoadUiSettings();
        SyncGridRunRootWithSelectedPersonality(updateEmbeddedGrid: false);
        LoadSampleAssets();
        SyncSelectedSampleAssetsToTextFields();
        ReflowLayout();
        RecreateEmbeddedGrid();
        RefreshActionAvailability();

        _btnSetup.Click += async (_, _) => await RunSetupAsync();
        _btnExtract.Click += async (_, _) => await RunExtractAsync();
        _btnDeploy.Click += async (_, _) => await RunDeployAsync();
        _btnUndeploy.Click += async (_, _) => await RunUndeployAsync();
        _btnPreview.Click += async (_, _) => await RunPreviewAsync();
        _btnSeedVcSettings.Click += (_, _) => EditSeedVcSettings();
        _btnCancel.Click += (_, _) => _cts?.Cancel();
        _btnPlayPreviewNormal.Click += async (_, _) => await PlayPreviewAsync(_lastPreviewNormalPath, _btnPlayPreviewNormal);
        _btnPlayPreviewEro.Click += async (_, _) => await PlayPreviewAsync(_lastPreviewEroPath, _btnPlayPreviewEro);
        _btnEditNormalSegment.Click += (_, _) => OpenRangeEditor(isEro: false);
        _btnEditEroSegment.Click += (_, _) => OpenRangeEditor(isEro: true);
        _btnClearNormalSegment.Click += (_, _) => ClearManualRange(isEro: false);
        _btnClearEroSegment.Click += (_, _) => ClearManualRange(isEro: true);
        _txtSourceHs2Root.TextChanged += (_, _) => RefreshActionAvailability();
        _txtDeployRoot.TextChanged += (_, _) => RefreshActionAvailability();
        _txtOutputRoot.Leave += (_, _) => ApplyOutputRootChangeFromUi(reloadSampleAssets: true);
        _cmbPersonality.SelectedIndexChanged += (_, _) =>
        {
            SyncGridRunRootWithSelectedPersonality(updateEmbeddedGrid: true);
            RefreshActionAvailability();
        };
        SizeChanged += (_, _) => ReflowLayout();
        FormClosing += (_, _) => SaveUiSettings();
    }
}


