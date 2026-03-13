using System.Globalization;
using System.Media;

namespace HS2VoiceReplace;

internal sealed partial class SampleRangeSelectorDialog
{
    private async Task LoadWaveDataAsync()
    {
        try
        {
            var loaded = await Task.Run(() =>
            {
                // Waveform analysis always operates on a normalized mono WAV temp file so UI
                // responsiveness does not depend on the user's original codec/container.
                var analysisWav = EnsureAnalysisWav(_sourceFile, _ffmpegExe);
                var env = BuildEnvelopeFromWav(analysisWav, 3200);
                return (analysisWav, env.Envelope, env.TotalSec, analysisWavTemp: !string.Equals(analysisWav, _sourceFile, StringComparison.OrdinalIgnoreCase));
            });

            if (loaded.analysisWavTemp)
                _analysisWavTemp = loaded.analysisWav;
            _envelope = loaded.Item2;
            _totalSec = loaded.TotalSec;
            if (_totalSec <= 0.01)
                throw new InvalidOperationException(UiTextCatalog.Get("error.waveformZeroLength"));

            if (_initialSelection != null && string.Equals(Path.GetFullPath(_initialSelection.SourceFile), _sourceFile, StringComparison.OrdinalIgnoreCase))
            {
                _numDuration.Value = ClampToDecimal((decimal)_initialSelection.DurationSec, _numDuration.Minimum, _numDuration.Maximum);
                ApplyDurationRange();
                _numStart.Value = ClampToDecimal((decimal)_initialSelection.StartSec, _numStart.Minimum, _numStart.Maximum);
                SyncTrackFromStart();
            }
            else
            {
                ApplyDurationRange();
                _numStart.Value = _numStart.Minimum;
                SyncTrackFromStart();
            }

            _waveReady = true;
            SetWaveControlsEnabled(true);
            _lblLoading.Text = T("dialog.rangeEditor.loadDone");
            UpdateRangeLabel();
            _waveBox.Invalidate();
        }
        catch (Exception ex)
        {
            _waveReady = false;
            SetWaveControlsEnabled(false);
            _lblLoading.Text = T("dialog.rangeEditor.loadFailed");
            MessageBox.Show(this, ex.Message, T("dialog.error.waveformLoad"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async Task PlaySelectionAsync()
    {
        try
        {
            if (!_waveReady)
                return;
            if (_isPlaying)
                StopPlayback();

            if (string.IsNullOrWhiteSpace(_ffmpegExe) || !File.Exists(_ffmpegExe))
                throw new InvalidOperationException(UiTextCatalog.Get("error.playSelectionNeedsFfmpeg"));

            DeleteTempPreviewClip();
            _previewClipTemp = Path.Combine(Path.GetTempPath(), $"hs2vr_preview_{Guid.NewGuid():N}.wav");
            var st = Math.Max(0, (double)_numStart.Value).ToString("0.###", CultureInfo.InvariantCulture);
            var du = Math.Max(0.2, (double)_numDuration.Value).ToString("0.###", CultureInfo.InvariantCulture);
            var args = $"-y -hide_banner -loglevel error -ss {st} -t {du} -i \"{_sourceFile}\" -vn -sn -dn -ac 1 -ar 32000 -f wav \"{_previewClipTemp}\"";

            _btnPlaySelection.Enabled = false;
            var r = await ProcessUtil.RunCaptureAsync(_ffmpegExe, args, Directory.GetCurrentDirectory(), CancellationToken.None);
            if (r.ExitCode != 0 || string.IsNullOrWhiteSpace(_previewClipTemp) || !File.Exists(_previewClipTemp))
                throw new InvalidOperationException(UiTextCatalog.Get("error.previewClipBuildFailed"));

            _previewPlayer = new SoundPlayer(_previewClipTemp);
            _previewPlayer.Load();
            _previewPlayer.Play();
            _isPlaying = true;
            _btnStopPlayback.Enabled = true;

            _ = Task.Run(async () =>
            {
                var ms = (int)(Math.Max(0.2, (double)_numDuration.Value) * 1000) + 120;
                await Task.Delay(ms);
                if (_disposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!_disposed)
                        {
                            _isPlaying = false;
                            _btnPlaySelection.Enabled = true;
                            _btnStopPlayback.Enabled = false;
                        }
                    }));
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            _isPlaying = false;
            _btnPlaySelection.Enabled = true;
            _btnStopPlayback.Enabled = false;
            MessageBox.Show(this, ex.Message, T("dialog.error.playback"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopPlayback()
    {
        try { _previewPlayer?.Stop(); } catch { }
        _previewPlayer = null;
        _isPlaying = false;
        _btnPlaySelection.Enabled = true;
        _btnStopPlayback.Enabled = false;
    }

    private void DrawWaveform(Graphics g, Rectangle rc)
    {
        g.Clear(Color.White);
        if (!_waveReady) return;
        if (_envelope.Length == 0 || _totalSec <= 0.01 || rc.Width < 4 || rc.Height < 4)
            return;

        using var penWave = new Pen(Color.FromArgb(50, 90, 170), 1f);
        using var penCenter = new Pen(Color.Gainsboro, 1f);
        using var selBrush = new SolidBrush(Color.FromArgb(70, 60, 180, 75));
        using var selBorder = new Pen(Color.FromArgb(60, 150, 65), 2f);

        var centerY = rc.Top + rc.Height / 2f;
        g.DrawLine(penCenter, rc.Left, centerY, rc.Right, centerY);

        for (int x = 0; x < rc.Width; x++)
        {
            var idx = (int)Math.Round((x / (double)Math.Max(1, rc.Width - 1)) * (_envelope.Length - 1));
            var amp = Math.Clamp(_envelope[idx], 0f, 1f);
            var h = amp * (rc.Height / 2f - 2f);
            g.DrawLine(penWave, rc.Left + x, centerY - h, rc.Left + x, centerY + h);
        }

        var st = (double)_numStart.Value;
        var ed = st + (double)_numDuration.Value;
        var x0 = rc.Left + (float)(Math.Clamp(st / _totalSec, 0.0, 1.0) * rc.Width);
        var x1 = rc.Left + (float)(Math.Clamp(ed / _totalSec, 0.0, 1.0) * rc.Width);
        if (x1 < x0) (x0, x1) = (x1, x0);
        var selRect = RectangleF.FromLTRB(x0, rc.Top + 1, x1, rc.Bottom - 1);
        g.FillRectangle(selBrush, selRect);
        g.DrawRectangle(selBorder, selRect.X, selRect.Y, selRect.Width, selRect.Height);
    }

    private static string EnsureAnalysisWav(string source, string? ffmpegExe)
    {
        if (!string.IsNullOrWhiteSpace(ffmpegExe) && File.Exists(ffmpegExe))
        {
            var outWav = Path.Combine(Path.GetTempPath(), $"hs2vr_wave_{Guid.NewGuid():N}.wav");
            var args = $"-y -hide_banner -loglevel error -i \"{source}\" -vn -sn -dn -ac 1 -ar 16000 -f wav \"{outWav}\"";
            var r = ProcessUtil.RunCaptureAsync(ffmpegExe, args, Directory.GetCurrentDirectory(), CancellationToken.None).GetAwaiter().GetResult();
            if (r.ExitCode != 0 || !File.Exists(outWav))
                throw new InvalidOperationException(UiTextCatalog.Get("error.waveformConversionFailed", r.StdErr));
            return outWav;
        }

        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext == ".wav")
            return source;
        throw new InvalidOperationException(UiTextCatalog.Get("error.waveformNeedsFfmpeg"));
    }

    private static (float[] Envelope, double TotalSec) BuildEnvelopeFromWav(string wavPath, int maxBins)
    {
        // This is intentionally a lightweight envelope builder, not a full waveform editor parser.
        // The dialog only needs a stable visual summary and rough seeking.
        using var fs = File.OpenRead(wavPath);
        using var br = new BinaryReader(fs);

        uint ReadU32() => br.ReadUInt32();
        ushort ReadU16() => br.ReadUInt16();

        if (ReadU32() != 0x46464952) throw new InvalidOperationException(UiTextCatalog.Get("error.notRiffWav"));
        _ = ReadU32();
        if (ReadU32() != 0x45564157) throw new InvalidOperationException(UiTextCatalog.Get("error.notWaveFormat"));

        ushort format = 1;
        ushort channels = 1;
        int sampleRate = 16000;
        ushort bits = 16;
        ushort blockAlign = 2;
        long dataPos = -1;
        int dataSize = 0;

        while (fs.Position + 8 <= fs.Length)
        {
            var id = ReadU32();
            var size = br.ReadInt32();
            if (size < 0 || fs.Position + size > fs.Length)
                throw new InvalidOperationException(UiTextCatalog.Get("error.invalidWavChunk"));

            if (id == 0x20746D66)
            {
                format = ReadU16();
                channels = ReadU16();
                sampleRate = br.ReadInt32();
                _ = br.ReadInt32();
                blockAlign = ReadU16();
                bits = ReadU16();
                var remain = size - 16;
                if (remain > 0) fs.Position += remain;
            }
            else if (id == 0x61746164)
            {
                dataPos = fs.Position;
                dataSize = size;
                fs.Position += size;
            }
            else
            {
                fs.Position += size;
            }

            if ((size & 1) == 1 && fs.Position < fs.Length)
                fs.Position += 1;
        }

        if (dataPos < 0 || dataSize <= 0)
            throw new InvalidOperationException(UiTextCatalog.Get("error.missingWavDataChunk"));
        if (channels < 1 || blockAlign <= 0 || sampleRate <= 0)
            throw new InvalidOperationException(UiTextCatalog.Get("error.invalidWavFormat"));

        var frames = dataSize / blockAlign;
        if (frames <= 0) return (Array.Empty<float>(), 0);
        var bins = Math.Clamp(maxBins, 512, 8192);
        bins = Math.Min(bins, Math.Max(256, frames / 16));
        var envelope = new float[bins];
        var framesPerBin = Math.Max(1, (int)Math.Ceiling(frames / (double)bins));

        fs.Position = dataPos;
        for (int i = 0; i < frames; i++)
        {
            float s = ReadSample(br, format, bits);
            var extra = blockAlign - Math.Max(1, bits / 8);
            if (extra > 0)
                fs.Position += extra;
            var a = Math.Abs(s);
            var idx = Math.Min(bins - 1, i / framesPerBin);
            if (a > envelope[idx]) envelope[idx] = a;
        }

        var totalSec = frames / (double)sampleRate;
        return (envelope, totalSec);
    }

    private static float ReadSample(BinaryReader br, ushort format, ushort bits)
    {
        if (format == 3 && bits == 32)
            return br.ReadSingle();

        if (format != 1)
            throw new InvalidOperationException(UiTextCatalog.Get("error.unsupportedWavFormat", format, bits));

        return bits switch
        {
            8 => (br.ReadByte() - 128) / 128f,
            16 => br.ReadInt16() / 32768f,
            24 => ReadInt24(br) / 8388608f,
            32 => br.ReadInt32() / 2147483648f,
            _ => throw new InvalidOperationException(UiTextCatalog.Get("error.unsupportedPcmBits", bits))
        };
    }

    private static int ReadInt24(BinaryReader br)
    {
        int b0 = br.ReadByte();
        int b1 = br.ReadByte();
        int b2 = br.ReadByte();
        int v = b0 | (b1 << 8) | (b2 << 16);
        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
        return v;
    }

    private static decimal ClampToDecimal(decimal v, decimal min, decimal max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }

    private static string FormatSec(double sec)
    {
        if (sec < 0) sec = 0;
        var t = TimeSpan.FromSeconds(sec);
        return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";
    }

    private void CleanupTemp()
    {
        if (_disposed) return;
        _disposed = true;
        StopPlayback();
        DeleteTempPreviewClip();
        try
        {
            if (!string.IsNullOrWhiteSpace(_analysisWavTemp) && File.Exists(_analysisWavTemp))
                File.Delete(_analysisWavTemp);
        }
        catch { }
    }

    private void DeleteTempPreviewClip()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_previewClipTemp) && File.Exists(_previewClipTemp))
                File.Delete(_previewClipTemp);
        }
        catch { }
        _previewClipTemp = null;
    }
}

