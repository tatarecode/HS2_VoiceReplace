using System.ComponentModel;
using System.Globalization;

namespace HS2VoiceReplace;

// Defines the sample-audio dialog composition point and coordinates its builder/action partials.

public sealed partial class MainForm
{
    private void OpenSampleAudioDialog()
    {
        if (_sampleAudioDialog is { IsDisposed: false })
        {
            RefreshSampleSignatureDisplay();
            UpdateBusySensitiveUi();
            _sampleAudioDialog.Show(this);
            _sampleAudioDialog.BringToFront();
            return;
        }

        var dlg = CreateSampleAudioDialogShell(out var root);
        var assign = CreateSampleAudioAssignPanel(out var cmbNormal, out var cmbEro);
        _sampleAudioAssignPanel = assign;
        root.Controls.Add(assign, 0, 0);

        var actions = CreateSampleAudioActionsPanel(
            out var btnAdd,
            out var btnRename,
            out var btnTrash,
            out var btnRestore,
            out var btnDeleteHard,
            out var btnPlayAsset,
            out var chkShowTrash);
        _sampleAudioActionsPanel = actions;
        root.Controls.Add(actions, 0, 1);

        var grid = CreateSampleAudioGrid();
        _sampleAudioGrid = grid;
        root.Controls.Add(grid, 0, 2);

        var bottom = CreateSampleAudioBottomPanel(dlg, out var btnClose);
        root.Controls.Add(bottom, 0, 3);

        var rows = new BindingList<SampleAssetGridRow>();
        grid.DataSource = rows;

        string? SelectedRowId()
        {
            if (grid.CurrentRow?.DataBoundItem is SampleAssetGridRow r)
                return r.Id;
            return null;
        }

        void FillRoleCombo(ComboBox cmb, string currentId)
        {
            cmb.BeginUpdate();
            cmb.Items.Clear();
            foreach (var a in _sampleAssets.Where(x => !x.IsDeleted).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                cmb.Items.Add(new ComboBoxItem(a.Id, $"{a.Name}  ({a.Signature[..Math.Min(10, a.Signature.Length)]})"));
            }
            if (cmb.Items.Count == 0)
            {
                cmb.EndUpdate();
                return;
            }
            var idx = -1;
            for (var i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is ComboBoxItem it && string.Equals(it.Value, currentId, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            cmb.SelectedIndex = idx >= 0 ? idx : 0;
            cmb.EndUpdate();
        }

        void RefreshAssetGrid()
        {
            EnsureSelectedSampleAssets();
            SyncSelectedSampleAssetsToTextFields();
            rows.RaiseListChangedEvents = false;
            rows.Clear();
            var view = _sampleAssets
                .Where(x => chkShowTrash.Checked || !x.IsDeleted)
                .OrderBy(x => x.IsDeleted)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var a in view)
            {
                rows.Add(new SampleAssetGridRow
                {
                    Id = a.Id,
                    Name = a.Name,
                    State = a.IsDeleted ? T("dialog.sampleAudio.state.trash") : T("dialog.sampleAudio.state.active"),
                    Signature = a.Signature,
                    HashAlgorithmVersion = a.HashAlgorithmVersion,
                    LengthSec = a.DurationSec.ToString("0.00", CultureInfo.InvariantCulture),
                    SampleRateHz = a.SampleRateHz.ToString(CultureInfo.InvariantCulture),
                    Channels = a.Channels.ToString(CultureInfo.InvariantCulture),
                    Source = a.SourceFilePath ?? "",
                    Range = (a.SourceStartSec.HasValue && a.SourceDurationSec.HasValue)
                        ? $"{a.SourceStartSec.Value:0.##}-{(a.SourceStartSec.Value + a.SourceDurationSec.Value):0.##}"
                        : "",
                    ExtractedAt = a.ExtractedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    LastUsedAt = a.LastUsedAtUtc.HasValue
                        ? a.LastUsedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        : "",
                });
            }
            rows.RaiseListChangedEvents = true;
            rows.ResetBindings();

            FillRoleCombo(cmbNormal, _normalSampleAssetId);
            FillRoleCombo(cmbEro, _eroSampleAssetId);
            RefreshSampleSignatureDisplay();
        }

        cmbNormal.SelectedIndexChanged += (_, _) => HandleSampleRoleChanged(cmbNormal, isEro: false);
        cmbEro.SelectedIndexChanged += (_, _) => HandleSampleRoleChanged(cmbEro, isEro: true);
        chkShowTrash.CheckedChanged += (_, _) => RefreshAssetGrid();

        btnAdd.Click += async (_, _) => await HandleSampleAssetAddAsync(btnAdd, RefreshAssetGrid);
        btnRename.Click += (_, _) => HandleSampleAssetRename(SelectedRowId(), RefreshAssetGrid);
        btnTrash.Click += (_, _) => HandleSampleAssetTrash(SelectedRowId(), RefreshAssetGrid);
        btnRestore.Click += (_, _) => HandleSampleAssetRestore(SelectedRowId(), RefreshAssetGrid);
        btnDeleteHard.Click += (_, _) => HandleSampleAssetDeletePermanent(SelectedRowId(), RefreshAssetGrid);
        btnPlayAsset.Click += async (_, _) => await HandleSampleAssetPlayAsync(SelectedRowId(), btnPlayAsset);

        RefreshAssetGrid();
        UpdateBusySensitiveUi();

        dlg.FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SaveSampleAssetsCatalog();
                SaveUiSettings();
                dlg.Hide();
            }
        };
        dlg.FormClosed += (_, _) =>
        {
            _lblSampleSignatureInDialog = null;
            _btnSampleDialogCancel = null;
            _sampleAudioAssignPanel = null;
            _sampleAudioActionsPanel = null;
            _sampleAudioGrid = null;
            if (ReferenceEquals(_sampleAudioDialog, dlg))
                _sampleAudioDialog = null;
        };
        _sampleAudioDialog = dlg;
        dlg.Show(this);
    }
}


