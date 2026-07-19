using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SplattingPipeline
{
    /// <summary>
    /// Wraps a single external process (COLMAP, FFmpeg, Brush, ...) and exposes
    /// its stdout/stderr as events plus an awaitable exit code.
    /// Output events fire on a background thread — anything touching Unity UI
    /// must be marshaled via MainThreadDispatcher.
    /// </summary>
    public class ProcessRunner
    {
        public event Action<string> OnOutputLine;   // stdout, line by line
        public event Action<string> OnErrorLine;     // stderr, line by line

        public async Task<int> RunAsync(
            string exePath,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) OnOutputLine?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) OnErrorLine?.Invoke(e.Data);
            };

            if (!process.Start())
                throw new InvalidOperationException($"Failed to start process: {exePath}");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Register cancellation: kill the OS process if the pipeline is cancelled.
            using var registration = cancellationToken.Register(() =>
            {
                // try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                try { if (!process.HasExited) ProcessTreeKiller.KillTree(process.Id); }
                catch { /* already exited, ignore */ }
            });

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }
    }

    // .NET's Process doesn't have a built-in cancellable WaitForExitAsync on all
    // target frameworks — this extension covers Unity's typical .NET Standard 2.1 target.
    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => tcs.TrySetResult(true);
            if (process.HasExited) return Task.CompletedTask;

            cancellationToken.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }
    }
}
