using System.Diagnostics;
using System.Text;

namespace HS2VoiceReplace;

// Wraps external process execution, stdout/stderr capture, and cancellation-aware logging for tool invocations.

internal static class ProcessUtil
{
    public readonly record struct CaptureResult(int ExitCode, string StdOut, string StdErr);

    public static async Task RunAsync(
        string exe,
        string args,
        string workingDir,
        Action<string> log,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (env != null)
            foreach (var kv in env)
                psi.Environment[kv.Key] = kv.Value;

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data!); };
        p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("[stderr] " + e.Data); };

        log($"> {exe} {args}");
        if (!p.Start()) throw new InvalidOperationException("Failed to start process: " + exe);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"process failed: {exe} (exit={p.ExitCode})");
    }

    public static async Task<CaptureResult> RunCaptureAsync(
        string exe,
        string args,
        string workingDir,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (env != null)
            foreach (var kv in env)
                psi.Environment[kv.Key] = kv.Value;

        using var p = new Process { StartInfo = psi };
        if (!p.Start())
            throw new InvalidOperationException("Failed to start process: " + exe);

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new CaptureResult(p.ExitCode, stdout, stderr);
    }
}


