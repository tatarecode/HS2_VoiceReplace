using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

// Provides the configuration dialog for Seed-VC-related settings exposed to the end user.

internal sealed class SeedVcSettingsDialog : Form
{
    private readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = false };
    public SeedVcUiSettings Settings { get; }

    public SeedVcSettingsDialog(SeedVcUiSettings settings)
    {
        Settings = settings;
        // Refresh the type descriptor before binding so category, display name, and
        // description strings are resolved in the current UI language.
        TypeDescriptor.Refresh(typeof(SeedVcUiSettings));
        Text = UiTextCatalog.Get("seedvc.dialog.title");
        StartPosition = FormStartPosition.CenterParent;
        UiSizeHelper.ApplyDialogSize(this, new Size(1080, 1040), new Size(920, 760), fixedSize: true);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _grid.PropertySort = PropertySort.Categorized;
        _grid.SelectedObject = Settings;
        root.Controls.Add(_grid, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = UiTextCatalog.Get("button.ok"), DialogResult = DialogResult.OK, Width = 120 };
        var cancel = new Button { Text = UiTextCatalog.Get("button.cancel"), DialogResult = DialogResult.Cancel, Width = 120 };
        UiSizeHelper.FitButton(ok, 90, 34);
        UiSizeHelper.FitButton(cancel, 100, 34);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}


