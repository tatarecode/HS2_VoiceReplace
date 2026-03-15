using System.Globalization;
using System.Media;

namespace HS2VoiceReplace;

// Contains user actions for the partial rebuild grid, including playback, per-row rebuild, and full-run commands.

internal sealed partial class PartialRebuildGridDialog
{
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

    private async Task OnGridCellContentClickAsync(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var row = _rows[e.RowIndex];
        var colName = _grid.Columns[e.ColumnIndex].Name;
        if (_isOwnerBusy())
            return;
        if (colName == "btnPlaySrc")
        {
            PlayWav(row.SourceFile, T("dialog.sampleAudio.column.source"));
            return;
        }
        if (colName == "btnPlayDst")
        {
            PlayWav(row.ConvertedFile, T("button.playDst"));
            return;
        }
        if (_busy)
            return;
        if (colName == "btnDiscardDst")
        {
            DiscardConverted(row);
            _grid.InvalidateRow(e.RowIndex);
            return;
        }
        if (colName != "btnRebuildRow")
            return;

        _busy = true;
        SetBusyControls();
        row.Status = T("dialog.partialGrid.status.rebuilding");
        _grid.InvalidateRow(e.RowIndex);

        try
        {
            row.Bucket = (row.Bucket ?? "").Trim().ToLowerInvariant() == "ero" ? "ero" : "normal";
            var rebuiltRel = row.RelativePath;
            var outWav = await _onRebuild(row);
            row.ConvertedFile = outWav;
            row.SourceExists = File.Exists(row.SourceFile);
            row.ConvertedExists = File.Exists(row.ConvertedFile);
            row.SampleSignatureUsed = row.Bucket == "ero" ? row.SampleSignatureEro : row.SampleSignatureNormal;
            SaveSampleSignatureMapForRun(row.RunRoot);
            ReloadRows();
            if (_rowByRel.TryGetValue(rebuiltRel, out var refreshedRow))
                refreshedRow.Status = T("dialog.partialGrid.status.doneAt", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            _lblStatus.Text = T("dialog.partialGrid.status.updatedPath", rebuiltRel);
            _onLog($"grid rebuild done: {rebuiltRel}");
        }
        catch (OperationCanceledException)
        {
            row.Status = T("dialog.partialGrid.status.cancelRequested");
            _lblStatus.Text = T("dialog.partialGrid.status.cancelRequested");
            _onLog("grid rebuild cancelled");
        }
        catch (Exception ex)
        {
            row.Status = T("dialog.partialGrid.status.failed");
            _lblStatus.Text = T("dialog.partialGrid.status.errorWithMessage", ex.Message);
            _onLog("grid rebuild failed: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("dialog.error.rebuild"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            SetBusyControls();
            _grid.InvalidateRow(e.RowIndex);
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

    private void DiscardConverted(PartialRebuildGridRow row)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(row.ConvertedFile) && File.Exists(row.ConvertedFile))
                File.Delete(row.ConvertedFile);
            row.ConvertedExists = false;
            row.Status = T("dialog.partialGrid.status.discardedAt", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
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
                row.ConvertedExists = File.Exists(row.ConvertedFile);
        }
        else if (line.Contains(" start ", StringComparison.OrdinalIgnoreCase))
        {
            if (progressIndex.HasValue)
                _relByProgressIndex[progressIndex.Value] = rel;
            row.Status = T("dialog.partialGrid.status.converting");
        }
        if (_rowIndexByRel.TryGetValue(rel, out var idx) && idx >= 0 && idx < _grid.Rows.Count)
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


