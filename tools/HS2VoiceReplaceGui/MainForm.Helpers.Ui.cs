using System.Globalization;

namespace HS2VoiceReplace;

// Provides generic UI helper routines for layout refresh, control state updates, and small reusable WinForms utilities.

public sealed partial class MainForm
{
    private string? PromptText(string title, string initial)
    {
        using var dlg = new Form
        {
            Text = title,
            Width = 560,
            Height = 170,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dlg.Controls.Add(root);
        root.Controls.Add(new Label { AutoSize = true, Text = T("dialog.prompt.enterName") }, 0, 0);
        var tb = new TextBox { Dock = DockStyle.Top, Text = initial ?? "" };
        root.Controls.Add(tb, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = T("button.ok"), DialogResult = DialogResult.OK, Width = 100, Height = 34 };
        var cancel = new Button { Text = T("button.cancel"), DialogResult = DialogResult.Cancel, Width = 100, Height = 34 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return null;
        var value = (tb.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value;
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), line);
            return;
        }
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }

    private void OpenRangeEditor(bool isEro)
    {
        if (_isBusy)
            return;
        try
        {
            var source = (isEro ? _txtEroSample.Text : _txtNormalSample.Text).Trim();
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException(UiTextCatalog.Get(_uiLanguage, "error.sampleFileNotFound"), source);

            var ffmpegExe = TryFindFfmpegExe();
            var current = isEro ? _manualEroSegment : _manualNormalSegment;
            if (current != null && !string.Equals(Path.GetFullPath(current.SourceFile), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                current = null;

            using var dlg = new SampleRangeSelectorDialog(source, current, ffmpegExe);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Selection == null)
                return;

            if (isEro)
            {
                _manualEroSegment = dlg.Selection.Clone();
                _txtEroSegment.Text = _manualEroSegment.ToShortString();
            }
            else
            {
                _manualNormalSegment = dlg.Selection.Clone();
                _txtNormalSegment.Text = _manualNormalSegment.ToShortString();
            }
            RefreshSampleSignatureDisplay();
            AppendLog(UiTextCatalog.Get(_uiLanguage, "log.sampleRangeUpdated", isEro ? "ero" : "normal", dlg.Selection.ToShortString()));
        }
        catch (Exception ex)
        {
            AppendLog(UiTextCatalog.Get(_uiLanguage, "log.rangeError", ex.Message));
            MessageBox.Show(this, ex.Message, T("dialog.error.range"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearManualRange(bool isEro)
    {
        if (_isBusy)
            return;
        if (isEro)
        {
            _manualEroSegment = null;
            _txtEroSegment.Text = string.Empty;
        }
        else
        {
            _manualNormalSegment = null;
            _txtNormalSegment.Text = string.Empty;
        }
        RefreshSampleSignatureDisplay();
    }

    private enum PathMode { Folder, File }

    private void AddPathRow(TableLayoutPanel panel, ref int row, string label, TextBox text, bool required, PathMode mode)
    {
        AddFieldLabel(panel, ref row, required ? label : label + T("core.optionalSuffix"));
        panel.Controls.Add(text, 0, row);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var btnBrowse = new Button { Text = T("button.browse"), Width = 130, Height = 36 };
        UiSizeHelper.FitButton(btnBrowse, 100, 36);
        btnBrowse.Click += (_, _) =>
        {
            if (mode == PathMode.Folder)
            {
                using var dlg = new FolderBrowserDialog { SelectedPath = text.Text };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    text.Text = dlg.SelectedPath;
            }
            else
            {
                using var dlg = new OpenFileDialog { FileName = text.Text, Filter = "All Files (*.*)|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    text.Text = dlg.FileName;
            }
        };

        var btnClear = new Button { Text = T("button.clearPath"), Width = 130, Height = 36 };
        UiSizeHelper.FitButton(btnClear, 100, 36);
        btnClear.Click += (_, _) => text.Text = string.Empty;
        buttonPanel.Controls.Add(btnBrowse);
        buttonPanel.Controls.Add(btnClear);

        panel.Controls.Add(buttonPanel, 1, row);
        row++;
    }

    private static void AddReadonlyRow(TableLayoutPanel panel, ref int row, string label, TextBox text)
    {
        AddFieldLabel(panel, ref row, label);
        panel.Controls.Add(text, 0, row);
        panel.Controls.Add(new Label { AutoSize = true }, 1, row);
        row++;
    }

    private static void AddSectionHeader(TableLayoutPanel panel, ref int row, string text)
    {
        var baseFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        var lbl = new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(baseFont, FontStyle.Bold),
            Margin = new Padding(0, row == 0 ? 0 : 10, 0, 2),
        };
        panel.Controls.Add(lbl, 0, row);
        panel.SetColumnSpan(lbl, 2);
        row++;
    }

    private static void AddFieldLabel(TableLayoutPanel panel, ref int row, string text)
    {
        var lbl = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 2) };
        panel.Controls.Add(lbl, 0, row);
        panel.SetColumnSpan(lbl, 2);
        row++;
    }

    private static void AddPreviewRow(TableLayoutPanel panel, ref int row, string label, TextBox text, Button playButton)
    {
        AddFieldLabel(panel, ref row, label);
        panel.Controls.Add(text, 0, row);
        var playPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        playPanel.Controls.Add(playButton);
        panel.Controls.Add(playPanel, 1, row);
        row++;
    }

    private static void AddSegmentRow(TableLayoutPanel panel, ref int row, string label, TextBox text, Button editButton, Button clearButton)
    {
        AddFieldLabel(panel, ref row, label);
        panel.Controls.Add(text, 0, row);
        var action = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        action.Controls.Add(editButton);
        action.Controls.Add(clearButton);
        panel.Controls.Add(action, 1, row);
        row++;
    }

    private string GetConfiguredHs2Root()
    {
        var source = _txtSourceHs2Root.Text.Trim();
        if (!string.IsNullOrWhiteSpace(source))
            return source;
        return _txtDeployRoot.Text.Trim();
    }

    private void SetConfiguredHs2Root(string? path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        _txtSourceHs2Root.Text = normalized;
        _txtDeployRoot.Text = normalized;
    }

    private void PromptForHs2RootIfMissingOnStartup()
    {
        if (_startupHs2RootPromptHandled)
            return;
        _startupHs2RootPromptHandled = true;
        if (!string.IsNullOrWhiteSpace(GetConfiguredHs2Root()))
            return;

        using var dlg = new FolderBrowserDialog
        {
            Description = T("dialog.hS2Root.pickOnStartup"),
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
            return;

        SetConfiguredHs2Root(dlg.SelectedPath);
        SaveUiSettings();
    }
}


