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
        
        private InputType _inputType;
        private PipelineSettings _settings;

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
        public async Task RunAsync(string inputPath, string workDir, InputType inputType, PipelineSettings settings)
        {
            _settings =  settings;
            _inputType = inputType;
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
                    // Folder of images 
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

            
            string args = $"-i \"{videoPath}\" -qscale:v 2 -vf fps={_settings.ExtractionFramerate} \"{Path.Combine(outDir, "frame_%05d.jpg")}\"";
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
            // sparse mapping into a single call 
            string args = $"automatic_reconstructor " +
                           $"--workspace_path \"{workDir}\" " +
                           $"--image_path \"{imagesDir}\" " +
                           $"--sparse 1 --dense 0";

            int exitCode = await runner.RunAsync(_colmapExePath, args, workDir, token);
            if (exitCode != 0)
                throw new Exception($"COLMAP reconstruction failed (exit code {exitCode}). " +
                                     "Common causes: too few overlapping images, or all frames too blurry to match.");
            if(_inputType == InputType.Images)
                CopyImages(workDir, imagesDir);
        }

        private void CopyImages(string workDir, string imagesDir)
        {
            string imagesFolder = Path.Combine(workDir, "images");
            Directory.CreateDirectory(imagesFolder);
            
            string[] images = Directory.GetFiles(imagesDir);
            foreach (var sourcePath in images)
            {
                string fileName = Path.GetFileName(sourcePath);
                string destFilePath = Path.Combine(imagesFolder, fileName);
                File.Copy(sourcePath, destFilePath);
            }
            
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
            string imagesDir = Path.Combine(workDir, "images");
            string outputDir = Path.Combine(workDir, "splat_output");
            Directory.CreateDirectory(outputDir);

            if (!Directory.Exists(imagesDir) || Directory.GetFiles(imagesDir).Length == 0)
                throw new Exception($"Images folder missing or empty at \"{imagesDir}\" — Brush needs the source images alongside the sparse reconstruction.");

            if (!Directory.Exists(sparseDir))
                throw new Exception($"Sparse reconstruction folder not found at \"{sparseDir}\".");

            var runner = new ProcessRunner();
            runner.OnOutputLine += line => HandleBrushLine(line);
            runner.OnErrorLine += line => HandleBrushLine(line);

            // Brush expects the dataset root (containing both "images/" and "sparse/0/"),
            // not the sparse folder alone.
            // string args = $"train \"{workDir}\" --export-path \"{outputDir}\"";
            string args = $"\"{workDir}\" --export-path \"{outputDir}\" --total-steps {_settings.NumIterations} --max-resolution {_settings.MaxResolution}";
            int exitCode = await runner.RunAsync(_brushExePath, args, workDir, token);

            if (exitCode != 0)
                throw new Exception($"Brush training failed (exit code {exitCode}).");

            // string plyPath = Path.Combine(outputDir, "export.ply");
            string plyPath = Path.Combine(outputDir, $"export_{_settings.NumIterations}.ply");
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
