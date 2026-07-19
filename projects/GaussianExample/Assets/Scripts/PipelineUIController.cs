using System.IO;
using UnityEngine;
// using UnityEngine.UI;
using UnityEngine.UIElements;

namespace SplattingPipeline
{
    /// <summary>
    /// Attach to a UI Canvas object. Wire up the public fields in the Inspector.
    /// This is the only place that touches Unity UI directly — all pipeline
    /// logic lives in SplattingPipeline.cs and knows nothing about Unity.
    /// </summary>
    public class PipelineUIController : MonoBehaviour
    {
        [Header("UI References")]
        public Button startButton;
        public Button cancelButton;
        public Slider progressBar;
        public Label statusLabel;
        public Label logConsole; // optional, can be left null

        [Header("Bundled Tool Paths (relative to StreamingAssets)")]
        public string colmapRelativePath = "tools/colmap/COLMAP.bat";
        public string ffmpegRelativePath = "tools/ffmpeg/ffmpeg.exe";
        public string brushRelativePath = "tools/brush/brush.exe";

        private SplattingPipeline _pipeline;
        private string _selectedInputPath;

        private void Awake()
        {
            string colmapPath = Path.Combine(Application.streamingAssetsPath, colmapRelativePath);
            string ffmpegPath = Path.Combine(Application.streamingAssetsPath, ffmpegRelativePath);
            string brushPath = Path.Combine(Application.streamingAssetsPath, brushRelativePath);

            _pipeline = new SplattingPipeline(colmapPath, ffmpegPath, brushPath);

            // Every event here fires on a background thread (it's forwarded from
            // ProcessRunner), so we hop back to the main thread before touching UI.
            _pipeline.OnStageChanged += stage =>
                MainThreadDispatcher.Instance.Enqueue(() => HandleStageChanged(stage));

            _pipeline.OnProgress += value =>
                MainThreadDispatcher.Instance.Enqueue(() => progressBar.value = value);

            _pipeline.OnLog += line =>
                MainThreadDispatcher.Instance.Enqueue(() => AppendLog(line));

            _pipeline.OnFinished += splatPath =>
                MainThreadDispatcher.Instance.Enqueue(() => HandleFinished(splatPath));

            _pipeline.OnFailed += error =>
                MainThreadDispatcher.Instance.Enqueue(() => HandleFailed(error));

            // startButton.onClick.AddListener(OnStartClicked);
            // cancelButton.onClick.AddListener(() => _pipeline.Cancel());
            // cancelButton.interactable = false;
        }

        // Call this from your file/folder picker once the user has chosen input.
        public void SetInputPath(string path)
        {
            _selectedInputPath = path;
            statusLabel.text = $"Ready: {Path.GetFileName(path)}";
        }

        private async void OnStartClicked()
        {
            if (string.IsNullOrEmpty(_selectedInputPath))
            {
                statusLabel.text = "Pick a video or image folder first.";
                return;
            }
            //
            // startButton.interactable = false;
            // cancelButton.interactable = true;
            progressBar.value = 0;

            string workDir = Path.Combine(Application.persistentDataPath, "jobs", System.Guid.NewGuid().ToString());

            // async void is acceptable here specifically because this is a top-level
            // UI event handler with its own error handling inside RunAsync.
            await _pipeline.RunAsync(_selectedInputPath, workDir);
        }

        private void HandleStageChanged(PipelineStage stage)
        {
            progressBar.value = 0;
            statusLabel.text = stage switch
            {
                PipelineStage.ExtractingFrames => "Extracting frames from video…",
                PipelineStage.RunningColmap => "Estimating camera positions (COLMAP)…",
                PipelineStage.TrainingBrush => "Training gaussian splat…",
                PipelineStage.Done => "Done!",
                PipelineStage.Cancelled => "Cancelled.",
                PipelineStage.Error => "Something went wrong.",
                _ => statusLabel.text
            };

            if (stage is PipelineStage.Done or PipelineStage.Error or PipelineStage.Cancelled)
            {
                // startButton.interactable = true;
                // cancelButton.interactable = false;
            }
        }

        private void HandleFinished(string splatPath)
        {
            statusLabel.text = $"Splat ready: {splatPath}";
            // TODO: hand splatPath off to your UnityGaussianSplatting viewer here,
            // e.g. GaussianSplatAssetImporter.Import(splatPath) or similar.
        }

        private void HandleFailed(string error)
        {
            statusLabel.text = $"Failed: {error}";
        }

        private void AppendLog(string line)
        {
            if (logConsole == null) return;
            logConsole.text += line + "\n";
        }
    }
}
