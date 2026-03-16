using System.Globalization;
using System.Media;

namespace HS2VoiceReplace;

// Contains user actions for the partial rebuild grid, including playback, per-row rebuild, and full-run commands.

internal sealed partial class PartialRebuildGridDialog
{
    private List<PartialRebuildGridRow> GetSelectedRows()
        => _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Where(x => x.DataBoundItem is PartialRebuildGridRow)
            .Select(x => (PartialRebuildGridRow)x.DataBoundItem!)
            .Distinct()
            .ToList();

    private async Task RunFullAsync()
    {
        if (_busy || _isOwnerBusy())
            return;
        _busy = true;
        _fullRunExecuting = true;
        _relByProgressIndex.Clear();
        SetBusyControls();
        _lblStatus.Text = T("dialog.partialGrid.status.runningFull");
        SetFullProgressMarquee();
        try
        {
            var runRoot = await _onRunFull(UpdateFullProgressFromLog);
            if (!string.IsNullOrWhiteSpace(runRoot))
                _txtRunRoot.Text = runRoot;
            ReloadRows();
            SetFullProgressCompleted();
            _lblStatus.Text = T("dialog.partialGrid.status.completedFull");
        }
        catch (OperationCanceledException)
        {
            SetFullProgressError();
            _lblStatus.Text = T("dialog.partialGrid.status.cancelledFull");
            _onLog("grid full run cancelled");
        }
        catch (Exception ex)
        {
            SetFullProgressError();
            _lblStatus.Text = T("dialog.partialGrid.status.errorWithMessage", ex.Message);
            _onLog("grid full run failed: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("dialog.error.fullConversion"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _fullRunExecuting = false;
            _busy = false;
            SetBusyControls();
        }
    }

    private void PlaySelectedSource()
    {
        var selected = GetSelectedRows();
        if (selected.Count != 1)
            return;
        PlayWav(selected[0].SourceFile, T("dialog.sampleAudio.column.source"));
    }

    private void PlaySelectedConverted()
    {
        var selected = GetSelectedRows();
        if (selected.Count != 1)
            return;
        PlayWav(selected[0].ConvertedFile, T("button.playDst"));
    }

    private async Task RebuildSelectedRowsAsync()
    {
        if (_busy || _isOwnerBusy())
            return;
        var selected = GetSelectedRows();
        if (selected.Count == 0)
            return;

        _busy = true;
        SetBusyControls();
        foreach (var row in selected)
        {
            row.Status = T("dialog.partialGrid.status.rebuilding");
            RefreshBoundRow(row);
        }

        var rebuilt = new List<string>();
        try
        {
            foreach (var row in selected)
            {
                row.Bucket = (row.Bucket ?? "").Trim().ToLowerInvariant() == "ero" ? "ero" : "normal";
                var outWav = await _onRebuild(row);
                row.ConvertedFile = outWav;
                row.SourceExists = File.Exists(row.SourceFile);
                row.ConvertedExists = File.Exists(row.ConvertedFile);
                if (row.ConvertedExists)
                    FillDisplayedConversionMetadata(row);
                RefreshBoundRow(row);
                rebuilt.Add(row.RelativePath);
            }
            ReloadRows();
            var now = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var rel in rebuilt)
            {
                if (_rowByRel.TryGetValue(rel, out var refreshedRow))
                {
                    refreshedRow.Status = T("dialog.partialGrid.status.doneAt", now);
                    RefreshBoundRow(refreshedRow);
                }
            }
            _lblStatus.Text = T("dialog.partialGrid.status.updatedRows", rebuilt.Count);
            _onLog($"grid rebuild done: rows={rebuilt.Count}");
        }
        catch (OperationCanceledException)
        {
            foreach (var row in selected)
            {
                row.Status = T("dialog.partialGrid.status.cancelRequested");
                RefreshBoundRow(row);
            }
            _lblStatus.Text = T("dialog.partialGrid.status.cancelRequested");
            _onLog("grid rebuild cancelled");
        }
        catch (Exception ex)
        {
            foreach (var row in selected)
            {
                row.Status = T("dialog.partialGrid.status.failed");
                RefreshBoundRow(row);
            }
            _lblStatus.Text = T("dialog.partialGrid.status.errorWithMessage", ex.Message);
            _onLog("grid rebuild failed: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("dialog.error.rebuild"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            SetBusyControls();
        }
    }

    private void UpdateFullProgressFromLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(UpdateFullProgressFromLog), line);
            return;
        }

        if (string.IsNullOrWhiteSpace(line))
            return;

        _lblStatus.Text = line;
        TryUpdateGridRowStatusFromProgressLine(line);

        var matches = FullRunProgressRegex.Matches(line);
        if (matches.Count == 0)
            return;

        var m = matches[^1];
        if (!int.TryParse(m.Groups["done"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var done))
            return;
        if (!int.TryParse(m.Groups["total"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            return;
        if (total <= 0)
            return;

        if (_pbFull.Style != ProgressBarStyle.Continuous)
        {
            _pbFull.Style = ProgressBarStyle.Continuous;
            _pbFull.MarqueeAnimationSpeed = 0;
        }

        if (_pbFull.Maximum != total)
            _pbFull.Maximum = total;
        _pbFull.Value = Math.Clamp(done, 0, total);
    }

    private void RequestStopFullRun()
    {
        if (!_fullRunExecuting || !_busy)
            return;
        if (_onCancelFull())
            _lblStatus.Text = T("dialog.partialGrid.status.cancelRequested");
        else
            _lblStatus.Text = T("dialog.partialGrid.status.noCancelableWork");
    }

    private void DiscardSelectedRows()
    {
        if (_busy || _isOwnerBusy())
            return;
        var selected = GetSelectedRows();
        if (selected.Count == 0)
            return;

        try
        {
            foreach (var row in selected)
                DiscardConverted(row);
            _lblStatus.Text = T("dialog.partialGrid.status.discardedRows", selected.Count);
            _grid.Invalidate();
        }
        catch
        {
        }
    }

    private void DiscardConverted(PartialRebuildGridRow row)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(row.ConvertedFile) && File.Exists(row.ConvertedFile))
                File.Delete(row.ConvertedFile);
            row.ConvertedExists = false;
            row.SampleNameUsed = "";
            row.SampleSignatureUsed = "";
            row.SeedVcSummary = "";
            row.Status = T("dialog.partialGrid.status.discardedAt", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            RefreshBoundRow(row);
            _lblStatus.Text = T("dialog.partialGrid.status.discardedPath", row.RelativePath);
            _onLog($"grid discard converted: {row.RelativePath}");
        }
        catch (Exception ex)
        {
            _lblStatus.Text = T("dialog.partialGrid.status.errorWithMessage", ex.Message);
            _onLog("grid discard failed: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("dialog.error.discard"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TryUpdateGridRowStatusFromProgressLine(string line)
    {
        var idxMatch = ProgressIndexRegex.Match(line);
        int? progressIndex = null;
        if (idxMatch.Success && int.TryParse(idxMatch.Groups["done"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idxParsed))
            progressIndex = idxParsed;

        string? rel = null;
        var relMatch = ProgressRelRegex.Match(line);
        if (relMatch.Success)
            rel = relMatch.Groups["rel"].Value.Replace('\\', '/');
        else if (progressIndex.HasValue && _relByProgressIndex.TryGetValue(progressIndex.Value, out var mappedRel))
            rel = mappedRel;

        if (string.IsNullOrWhiteSpace(rel))
            return;
        if (!_rowByRel.TryGetValue(rel, out var row))
            return;

        var statusMatch = ProgressStatusRegex.Match(line);
        if (statusMatch.Success)
        {
            var raw = statusMatch.Groups["status"].Value;
            row.Status = BuildDisplayStatus(raw, File.Exists(row.ConvertedFile));
            if (raw is "ok" or "fallback_src" or "fallback_silence")
            {
                row.ConvertedExists = File.Exists(row.ConvertedFile);
                if (row.ConvertedExists)
                    FillDisplayedConversionMetadata(row);
            }
            RefreshBoundRow(row);
        }
        else if (line.Contains(" start ", StringComparison.OrdinalIgnoreCase))
        {
            if (progressIndex.HasValue)
                _relByProgressIndex[progressIndex.Value] = rel;
            row.Status = T("dialog.partialGrid.status.converting");
            RefreshBoundRow(row);
        }
    }

    private static void FillDisplayedConversionMetadata(PartialRebuildGridRow row)
    {
        row.SampleSignatureUsed = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase)
            ? row.SampleSignatureEro
            : row.SampleSignatureNormal;
        row.SampleNameUsed = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase)
            ? row.SampleNameEro
            : row.SampleNameNormal;
        row.SeedVcSummary = row.SeedVcSummaryStored;
    }

    private void RefreshBoundRow(PartialRebuildGridRow row)
    {
        if (!_rowIndexByRel.TryGetValue(row.RelativePath, out var idx))
            return;
        if (idx < 0 || idx >= _rows.Count)
            return;

        _rows.ResetItem(idx);
        if (idx < _grid.Rows.Count)
            _grid.InvalidateRow(idx);
    }

    private void PlayWav(string path, string title)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(T("message.notFound", title), path);
            _activePlayer?.Stop();
            _activePlayer?.Dispose();
            _activePlayer = new SoundPlayer(path);
            _activePlayer.Load();
            _activePlayer.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("dialog.error.playback"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnGridCellFormatting(DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
        var row = _rows[e.RowIndex];
        if (!row.SourceExists || (!row.ConvertedExists && _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(PartialRebuildGridRow.ConvertedFile)))
        {
            var style = e.CellStyle ?? new DataGridViewCellStyle();
            style.ForeColor = Color.Firebrick;
            e.CellStyle = style;
        }
    }
}


