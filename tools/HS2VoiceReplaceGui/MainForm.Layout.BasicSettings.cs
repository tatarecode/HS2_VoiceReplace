namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    private void OpenBasicSettingsDialog()
    {
        if (_basicSettingsDialog is { IsDisposed: false })
        {
            _basicSettingsDialog.Show(this);
            _basicSettingsDialog.BringToFront();
            return;
        }

        var dlg = new Form
        {
            Text = T("general.title"),
            Width = 980,
            Height = 620,
            MinimumSize = new Size(840, 520),
            StartPosition = FormStartPosition.CenterParent,
        };
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
        dlg.Controls.Add(host);
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        var row = 0;
        AddReadonlyRow(table, ref row, T("general.bundledRuntimeRoot"), _txtBundleRoot);
        AddPathRow(table, ref row, T("general.outputRoot"), _txtOutputRoot, false, PathMode.Folder);
        AddPathRow(table, ref row, T("general.externalToolsRoot"), _txtExternalToolsRoot, false, PathMode.Folder);
        AddPathRow(table, ref row, T("general.sourceHs2Root"), _txtSourceHs2Root, true, PathMode.Folder);
        AddPathRow(table, ref row, T("general.deployHs2Root"), _txtDeployRoot, true, PathMode.Folder);
        AddFieldLabel(table, ref row, T("general.uiLanguage"));
        var langPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var cmbLang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        cmbLang.Items.Add(T("general.languageJapanese"));
        cmbLang.Items.Add(T("general.languageEnglish"));
        cmbLang.SelectedIndex = _uiLanguage == UiLanguage.En ? 1 : 0;
        cmbLang.SelectedIndexChanged += (_, _) =>
        {
            var selected = cmbLang.SelectedIndex == 1 ? UiLanguage.En : UiLanguage.Ja;
            if (selected == _uiLanguage)
                return;

            if (_cts != null)
            {
                MessageBox.Show(
                    this,
                    T("general.languageBusyMessage"),
                    T("general.languageBusyTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                cmbLang.SelectedIndex = _uiLanguage == UiLanguage.En ? 1 : 0;
                return;
            }

            // Run after current ComboBox event finishes to avoid disposing the current dialog in-handler.
            BeginInvoke(new Action(() =>
            {
                try
                {
                    ChangeUiLanguage(selected);
                }
                catch (Exception ex)
                {
                    AppendLog(UiTextCatalog.Get(_uiLanguage, "log.uiLanguageSwitch", ex.Message));
                    MessageBox.Show(
                        this,
                        ex.ToString(),
                        T("general.languageSwitchError"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }));
        };
        langPanel.Controls.Add(cmbLang);
        table.Controls.Add(langPanel, 0, row);
        table.SetColumnSpan(langPanel, 2);
        row++;

        AddFieldLabel(table, ref row, T("general.executionOptions"));
        var opt = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        opt.Controls.Add(_chkSkipCompleted);
        table.Controls.Add(opt, 0, row);
        table.SetColumnSpan(opt, 2);
        row++;
        var close = new Button { Text = T("button.close"), Width = 130, Height = 36 };
        UiSizeHelper.FitButton(close, 110, 36);
        close.Click += (_, _) => dlg.Hide();
        table.Controls.Add(close, 0, row);
        table.SetColumnSpan(close, 2);
        host.Controls.Add(table);
        dlg.FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                dlg.Hide();
            }
        };
        _basicSettingsDialog = dlg;
        dlg.Show(this);
    }

}

