using System.ComponentModel;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Compose the stable top-level shell: menu, header, grid host, and log pane.
    private void BuildLayout()
    {
        Controls.Clear();
        _menu.Items.Clear();
        _headerHost.Controls.Clear();
        var settingsMenu = new ToolStripMenuItem(T("menu.settings"));
        var menuBasic = new ToolStripMenuItem(T("menu.general"));
        var menuSample = new ToolStripMenuItem(T("menu.sampleAudio"));
        var menuConvert = new ToolStripMenuItem(T("menu.conversion"));
        menuBasic.Click += (_, _) => OpenBasicSettingsDialog();
        menuSample.Click += (_, _) => OpenSampleAudioDialog();
        menuConvert.Click += (_, _) => EditSeedVcSettings();
        settingsMenu.DropDownItems.Add(menuBasic);
        settingsMenu.DropDownItems.Add(menuSample);
        settingsMenu.DropDownItems.Add(menuConvert);
        _menu.Items.Add(settingsMenu);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 8, 10, 8),
            ColumnCount = 4,
            RowCount = 3,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblPid = new Label { Text = T("core.targetPersonality"), AutoSize = true, Anchor = AnchorStyles.Left };
        header.Controls.Add(lblPid, 0, 0);
        header.Controls.Add(_cmbPersonality, 1, 0);
        var btnFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        btnFlow.Controls.Add(_btnSetup);
        btnFlow.Controls.Add(_btnExtract);
        btnFlow.Controls.Add(_btnDeploy);
        btnFlow.Controls.Add(_btnUndeploy);
        btnFlow.Controls.Add(_btnCancel);
        header.Controls.Add(btnFlow, 3, 0);

        _lblSeedVcSummary.AutoSize = true;
        _lblSampleSignature.AutoSize = true;
        var lblSummary = new Label { Text = T("core.conversionSummary"), AutoSize = true, Anchor = AnchorStyles.Left };
        var lblSig = new Label { Text = T("core.sampleSignature"), AutoSize = true, Anchor = AnchorStyles.Left };
        header.Controls.Add(lblSummary, 0, 1);
        header.Controls.Add(_lblSeedVcSummary, 1, 1);
        header.SetColumnSpan(_lblSeedVcSummary, 3);
        header.Controls.Add(lblSig, 0, 2);
        header.Controls.Add(_lblSampleSignature, 1, 2);
        header.SetColumnSpan(_lblSampleSignature, 3);
        _headerHost.Controls.Add(header);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 720,
            Panel1MinSize = 380,
            Panel2MinSize = 140,
            FixedPanel = FixedPanel.None,
        };
        _mainSplit = split;
        Controls.Add(split);
        Controls.Add(_headerHost);
        Controls.Add(_menu);
        split.Panel1.Controls.Add(_gridHost);
        split.Panel2.Controls.Add(_txtLog);
        MainMenuStrip = _menu;
    }

}

