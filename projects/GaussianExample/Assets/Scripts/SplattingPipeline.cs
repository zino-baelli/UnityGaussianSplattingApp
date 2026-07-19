using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SplattingPipeline
{
    public enum PipelineStage
    {
        Idle,
        ExtractingFrames,
        RunningColmap,
        TrainingBrush,
        Done,
        Error,
        Cancelled
    }

    /// <summary>
    /// Plain C# class (not a MonoBehaviour) — owns the actual pipeline logic.
    /// A thin MonoBehaviour (PipelineUIController) drives this and reflects
    /// its events into UI. Keeping this UI-agnostic makes it testable and
    /// reusable outside Unity too.
    /// </summary>
    public class SplattingPipeline
    {
        // --- Events consumed by the UI layer ---
        public event Action<PipelineStage> OnStageChanged;
        public event Action<float> OnProgress;         // 0..1 within current stage
        public event Action<string> OnLog;              // raw lines, for a debug console
        public event Action<string> OnFinished;         // path to final splat file
        public event Action<string> OnFailed;            // error message

        private readonly string _colmapExePath;
        private readonly string _ffmpegExePath;
        private readonly string _brushExePath;

        private CancellationTokenSource _cts;
        public PipelineStage CurrentStage { get; private set; } = PipelineStage.Idle;

        public SplattingPipeline(string colmapExePath, string ffmpegExePath, string brushExePath)
        {
            _colmapExePath = colmapExePath;
            _ffmpegExePath = ffmpegExePath;
            _brushExePath = brushExePath;
        }

        public void Cancel() => _cts?.Cancel();

        /// <param name="inputPath">A video file OR a folder of images.</param>
        /// <param name="workDir">Scratch directory for this job (frames, COLMAP db, sparse model, splat output).</param>
        public async Task RunAsync(string inputPath, string workDir)
        {
            _cts = new CancellationTokenSource();
            Directory.CreateDirectory(workDir);
            var imagesDir = Path.Combine(workDir, "images");
            Directory.CreateDirectory(imagesDir);

            try
            {
                bool isVideo = File.Exists(inputPath);

                if (isVideo)
                {
                    SetStage(PipelineStage.ExtractingFrames);
                    await ExtractFramesAsync(inputPath, imagesDir, _cts.Token);
                }
                else
                {
                    // Folder of images — just point COLMAP at it directly.
                    imagesDir = inputPath;
                }

                SetStage(PipelineStage.RunningColmap);
                await RunColmapAsync(imagesDir, workDir, _cts.Token);

                SetStage(PipelineStage.TrainingBrush);
                string splatPath = await RunBrushAsync(workDir, _cts.Token);

                SetStage(PipelineStage.Done);
                OnFinished?.Invoke(splatPath);
            }
            catch (OperationCanceledException)
            {
                SetStage(PipelineStage.Cancelled);
            }
            catch (Exception ex)
            {
                SetStage(PipelineStage.Error);
                OnFailed?.Invoke(ex.Message);
            }
        }

        private void SetStage(PipelineStage stage)
        {
            CurrentStage = stage;
            OnStageChanged?.Invoke(stage);
        }

        private async Task ExtractFramesAsync(string videoPath, string outDir, CancellationToken token)
        {
            var runner = new ProcessRunner();
            runner.OnOutputLine += line => OnLog?.Invoke(line);
            runner.OnErrorLine += line => OnLog?.Invoke(line); // ffmpeg logs progress to stderr

            // 2 fps is a reasonable starting default; expose this later as a setting.
            string args = $"-i \"{videoPath}\" -qscale:v 2 -vf fps=2 \"{Path.Combine(outDir, "frame_%05d.jpg")}\"";
            int exitCode = await runner.RunAsync(_ffmpegExePath, args, outDir, token);

            if (exitCode != 0)
                throw new Exception($"FFmpeg frame extraction failed (exit code {exitCode}).");
        }

        private async Task RunColmapAsync(string imagesDir, string workDir, CancellationToken token)
        {
            string dbPath = Path.Combine(workDir, "database.db");
            string sparseDir = Path.Combine(workDir, "sparse");
            Directory.CreateDirectory(sparseDir);

            var runner = new ProcessRunner();
            runner.OnOutputLine += line => HandleColmapLine(line);
            runner.OnErrorLine += line => HandleColmapLine(line);

            // automatic_reconstructor bundles feature extraction, matching, and
            // sparse mapping into a single call — simpler than driving each step
            // yourself, at the cost of a little control over individual flags.
            string args = $"automatic_reconstructor " +
                           $"--workspace_path \"{workDir}\" " +
                           $"--image_path \"{imagesDir}\" " +
                           $"--sparse 1 --dense 0";

            int exitCode = await runner.RunAsync(_colmapExePath, args, workDir, token);
            if (exitCode != 0)
                throw new Exception($"COLMAP reconstruction failed (exit code {exitCode}). " +
                                     "Common causes: too few overlapping images, or all frames too blurry to match.");
        }

        private static readonly Regex ColmapProgressRegex = new Regex(@"Registered image #\d+ \((\d+) / (\d+)\)");

        private void HandleColmapLine(string line)
        {
            OnLog?.Invoke(line);
            var match = ColmapProgressRegex.Match(line);
            if (match.Success)
            {
                float current = float.Parse(match.Groups[1].Value);
                float total = float.Parse(match.Groups[2].Value);
                OnProgress?.Invoke(current / total);
            }
        }

        private static readonly Regex BrushProgressRegex = new Regex(@"step[:\s]+(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase);

        private async Task<string> RunBrushAsync(string workDir, CancellationToken token)
        {
            string sparseDir = Path.Combine(workDir, "sparse", "0");
            string outputDir = Path.Combine(workDir, "splat_output");
            Directory.CreateDirectory(outputDir);

            var runner = new ProcessRunner();
            runner.OnOutputLine += line => HandleBrushLine(line);
            runner.OnErrorLine += line => HandleBrushLine(line);

            string args = $"train \"{sparseDir}\" --export-path \"{outputDir}\"";
            int exitCode = await runner.RunAsync(_brushExePath, args, workDir, token);

            if (exitCode != 0)
                throw new Exception($"Brush training failed (exit code {exitCode}).");

            string plyPath = Path.Combine(outputDir, "export.ply");
            if (!File.Exists(plyPath))
                throw new Exception("Brush finished but no export.ply was found — check its CLI output for the actual filename.");

            return plyPath;
        }

        private void HandleBrushLine(string line)
        {
            OnLog?.Invoke(line);
            var match = BrushProgressRegex.Match(line);
            if (match.Success)
            {
                float current = float.Parse(match.Groups[1].Value);
                float total = float.Parse(match.Groups[2].Value);
                OnProgress?.Invoke(current / total);
            }
        }
    }
}
