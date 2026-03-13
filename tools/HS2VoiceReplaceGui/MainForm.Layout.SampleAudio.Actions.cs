namespace HS2VoiceReplace;

// Contains event handlers and commands for the sample-audio dialog so layout code stays separate from behavior.

public sealed partial class MainForm
{
    private void HandleSampleRoleChanged(ComboBox comboBox, bool isEro)
    {
        if (comboBox.SelectedItem is not ComboBoxItem item)
            return;

        if (isEro)
            _eroSampleAssetId = item.Value;
        else
            _normalSampleAssetId = item.Value;

        EnsureSelectedSampleAssets();
        SyncSelectedSampleAssetsToTextFields();
        SaveSampleAssetsCatalog();
        SaveUiSettings();
        RefreshSampleSignatureDisplay();
    }

    private async Task HandleSampleAssetAddAsync(Button triggerButton, Action refresh)
    {
        try
        {
            using var ofd = new OpenFileDialog { Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.m4a;*.aac|All Files|*.*" };
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;
            var source = ofd.FileName;
            using var picker = new SampleRangeSelectorDialog(source, null, TryFindFfmpegExe());
            if (picker.ShowDialog(this) != DialogResult.OK || picker.Selection == null)
                return;
            var defaultName = Path.GetFileNameWithoutExtension(source);
            var name = PromptText(T("dialog.sampleAudio.prompt.name"), defaultName);
            if (name == null)
                return;
            triggerButton.Enabled = false;
            UseWaitCursor = true;
            var item = await ImportSampleAssetInternalAsync(source, name, picker.Selection, silent: false);
            if (string.IsNullOrWhiteSpace(_normalSampleAssetId))
                _normalSampleAssetId = item.Id;
            if (string.IsNullOrWhiteSpace(_eroSampleAssetId))
                _eroSampleAssetId = item.Id;
            SaveUiSettings();
            refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.add"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            triggerButton.Enabled = true;
        }
    }

    private void HandleSampleAssetRename(string? id, Action refresh)
    {
        try
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;
            var next = PromptText(T("dialog.sampleAudio.prompt.newName"), item.Name);
            if (next == null)
                return;
            item.Name = next.Trim();
            item.UpdatedAtUtc = DateTime.UtcNow;
            SaveSampleAssetsCatalog();
            refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.rename"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleSampleAssetTrash(string? id, Action refresh)
    {
        try
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.IsDeleted)
                return;
            var src = GetAssetAbsolutePath(item);
            var dst = Path.Combine(SampleAssetsTrashRoot, $"{item.Id}.wav");
            Directory.CreateDirectory(SampleAssetsTrashRoot);
            if (File.Exists(src))
            {
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            item.RelativeWavPath = ToRelativeUnderOutput(dst);
            item.IsDeleted = true;
            item.DeletedAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
            if (string.Equals(_normalSampleAssetId, item.Id, StringComparison.OrdinalIgnoreCase)) _normalSampleAssetId = "";
            if (string.Equals(_eroSampleAssetId, item.Id, StringComparison.OrdinalIgnoreCase)) _eroSampleAssetId = "";
            EnsureSelectedSampleAssets();
            SaveSampleAssetsCatalog();
            SaveUiSettings();
            refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.delete"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleSampleAssetRestore(string? id, Action refresh)
    {
        try
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null || !item.IsDeleted)
                return;
            var src = GetAssetAbsolutePath(item);
            var dst = Path.Combine(SampleAssetsActiveRoot, $"{item.Id}.wav");
            Directory.CreateDirectory(SampleAssetsActiveRoot);
            if (File.Exists(src))
            {
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            item.RelativeWavPath = ToRelativeUnderOutput(dst);
            item.IsDeleted = false;
            item.DeletedAtUtc = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            EnsureSelectedSampleAssets();
            SaveSampleAssetsCatalog();
            SaveUiSettings();
            refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.restore"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleSampleAssetDeletePermanent(string? id, Action refresh)
    {
        try
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;
            var q = MessageBox.Show(
                this,
                T("dialog.sampleAudio.confirm.deletePermanent"),
                T("dialog.confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (q != DialogResult.Yes)
                return;
            var p = GetAssetAbsolutePath(item);
            if (File.Exists(p))
                File.Delete(p);
            _sampleAssets.Remove(item);
            if (string.Equals(_normalSampleAssetId, item.Id, StringComparison.OrdinalIgnoreCase)) _normalSampleAssetId = "";
            if (string.Equals(_eroSampleAssetId, item.Id, StringComparison.OrdinalIgnoreCase)) _eroSampleAssetId = "";
            EnsureSelectedSampleAssets();
            SaveSampleAssetsCatalog();
            SaveUiSettings();
            refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.deletePermanent"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task HandleSampleAssetPlayAsync(string? id, Button playButton)
    {
        try
        {
            var item = _sampleAssets.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;
            await PlayPreviewAsync(GetAssetAbsolutePath(item), playButton);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.play"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}


