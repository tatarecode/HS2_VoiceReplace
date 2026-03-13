using System.Media;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Preview playback stays outside the main conversion pipeline so users can audition assets
    // without mutating run state.
    private async Task PlayPreviewAsync(string wavPath, Control? disableWhilePlaying = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
                throw new FileNotFoundException(T("error.previewWavMissing"), wavPath);
            if (disableWhilePlaying != null)
                disableWhilePlaying.Enabled = false;
            await Task.Run(() =>
            {
                using var player = new SoundPlayer(wavPath);
                player.Load();
                player.PlaySync();
            });
            AppendLog(UiTextCatalog.Get(_uiLanguage, "log.playback", wavPath));
        }
        catch (Exception ex)
        {
            AppendLog(UiTextCatalog.Get(_uiLanguage, "log.playbackFailed", ex.Message));
            MessageBox.Show(this, ex.Message, T("dialog.error.playback"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (disableWhilePlaying != null && !disableWhilePlaying.IsDisposed)
                disableWhilePlaying.Enabled = true;
        }
    }

    private string? TryFindFfmpegExe()
    {
        var roots = new List<string>();
        void AddRoot(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                var full = Path.GetFullPath(p);
                if (!roots.Contains(full, StringComparer.OrdinalIgnoreCase))
                    roots.Add(full);
            }
            catch { }
        }

        AddRoot(_txtExternalToolsRoot.Text.Trim());
        AddRoot(_bundleRootFixed);
        AddRoot(Directory.GetCurrentDirectory());
        AddRoot(AppContext.BaseDirectory);

        var relCandidates = new[]
        {
            Path.Combine("ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine("_tools", "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine("_deps", "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine("rvc_webui", "_deps", "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine("_tools", "rvc_webui", "_deps", "ffmpeg", "bin", "ffmpeg.exe"),
        };

        foreach (var root in roots)
        {
            foreach (var rel in relCandidates)
            {
                var p = Path.Combine(root, rel);
                if (File.Exists(p))
                    return p;
            }
        }
        return null;
    }
}

