using System.IO;
using System.Linq;
using SFB;
using SplattingPipeline;
using UnityEngine;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;
using Button = UnityEngine.UIElements.Button;


public class UIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDoc;
    private VisualElement root;
    private bool isInputSelected = false;
    private bool isProcessRunning = false;
    
    private SplattingPipeline.SplattingPipeline _pipeline;
    private string _selectedInputPath;
    private InputType _selectedInputType;
    private string _selectedOutputPath;
    
    [Header("Bundled Tool Paths (relative to StreamingAssets)")]
    private string colmapRelativePath = "Tools/Colmap/COLMAP.bat";
    private string ffmpegRelativePath = "Tools/ffmpeg-8.1.2-essentials_build/bin/ffmpeg.exe";
    private string brushRelativePath = "Tools/brush-app-x86_64-pc-windows-msvc/brush_app.exe";
    
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
    private static readonly string[] VideoExtensions  = { ".mp4", ".mov", ".avi", ".webm", ".mkv" };

    private Label _inputLabel;
    private Label _outputLabel;
    private Label logConsole = null;
    private Label _pipelineStage;
    private Button _startButton;
    private Button _cancelButton;
    private Button _selectButton;
    private Button _selectOutputButton;
    private ProgressBar _progressBar;

    void Awake()
    {
        root = uiDoc.GetComponent<UIDocument>().rootVisualElement;
        _selectButton = root.Q<Button>("InputSelection");
        _selectButton.clickable.clicked += InputSelectionButtonClicked;
        _selectOutputButton = root.Q<Button>("OutputSelection");
        _selectOutputButton.clicked += SelectOutputButtonClicked;
        _startButton = root.Q<Button>("StartButton");
        _startButton.clicked += StartButtonClicked;
        
        _inputLabel =  root.Q<Label>("InputLabel");
        _outputLabel = root.Q<Label>("OutputLabel");
        _pipelineStage = root.Q<Label>("PipelineStage");
        
        _progressBar = root.Q<ProgressBar>("StageProgressBar");
        _progressBar.lowValue = 0f;
        _progressBar.highValue = 1f;
        
        string colmapPath = Path.Combine(Application.streamingAssetsPath, colmapRelativePath);
        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, ffmpegRelativePath);
        string brushPath = Path.Combine(Application.streamingAssetsPath, brushRelativePath);
        
        _pipeline = new SplattingPipeline.SplattingPipeline(colmapPath, ffmpegPath, brushPath);

        _pipeline.OnStageChanged += stage =>
            MainThreadDispatcher.Instance.Enqueue(() => HandleStageChanged(stage));

        _pipeline.OnProgress += value =>
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                _progressBar.value = value;
                _progressBar.title = $"{value * 100f:0}%";
            });

        _pipeline.OnLog += line =>
            MainThreadDispatcher.Instance.Enqueue(() => AppendLog(line));

        _pipeline.OnFinished += splatPath =>
            MainThreadDispatcher.Instance.Enqueue(() => HandleFinished(splatPath));

        _pipeline.OnFailed += error =>
            MainThreadDispatcher.Instance.Enqueue(() => HandleFailed(error));
   
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void InputSelectionButtonClicked()
    {
        
        var extensions = new[] {
            new ExtensionFilter("Media Files", "png", "jpg", "jpeg", "mp4", "mov", "avi", "mkv", "webm"),
        };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Images or Video", "", extensions, true);
        if (Validate( out var error, paths))
        {
            _inputLabel.style.color = Color.black;
            _inputLabel.text = paths[0];
            if (_selectedInputType == InputType.Images)
            {
                _selectedInputPath = Path.GetDirectoryName(paths[0]).ToString();
            }
            else
            {
                _selectedInputPath = paths[0];
            }
           
            return;
        }
        else
        {
            _inputLabel.style.color = Color.red;
            _inputLabel.text = error;
        }

            

    }

    private void SelectOutputButtonClicked()
    {
        string[] outputDirectory = StandaloneFileBrowser.OpenFolderPanel("Select Output Directory", "",false);
        _selectedOutputPath = outputDirectory[0];
        _outputLabel.text = _selectedOutputPath;
    }
    
   

    private bool Validate(out string error, string[] paths)
    {
        var types = paths.Select(Path.GetExtension).ToArray();
        var containsVideos = types.Any(type => VideoExtensions.Contains(type));
        var containsImages = types.Any(type => ImageExtensions.Contains(type));
        if (!containsVideos && !containsImages)
        {
            error = "Please select one video or multiple images";
            return false;
        }
        if (containsVideos && containsImages)
        {
            error = "Please select either a video or images";
            return false;
        }

        if (containsVideos)
        {
            if (paths.Length > 1)
            {
                error = "Please select only a single video";
                return false;
            }
            _selectedInputType = InputType.Video;
            error = "";
            return true;
        }
        
        _selectedInputType = InputType.Images;
        error = "";
        return true;
  
    }
    
    private void AppendLog(string line)
    {
        if (logConsole == null) return;
        logConsole.text += line + "\n";
    }

    private async void StartButtonClicked()
    {
         if (_selectedInputPath == null || _selectedOutputPath == null)
        {
            _pipelineStage.text = "Please select an Input and an Output path";
            return;
        }
        
        // if (!isInputSelected)
        // {
        //     //todo add no input message
        //     return;
        // }
        // if (!isProcessRunning) return;
        // string workDir = Path.Combine(Application.persistentDataPath, "jobs", System.Guid.NewGuid().ToString());
        string workDir = Path.Combine(_selectedOutputPath, "jobs", System.Guid.NewGuid().ToString());
        // _pipelineStage.text = workDir;
        await _pipeline.RunAsync(_selectedInputPath, workDir);
        // await _pipeline.RunAsync(_selectedInputPath,_selectedOutputPath);
    }
    
    private void HandleFinished(string splatPath)
    {
        _pipelineStage.text = $"Splat ready: {splatPath}";
        // TODO: hand splatPath off to your UnityGaussianSplatting viewer here,
        // e.g. GaussianSplatAssetImporter.Import(splatPath) or similar.
    }
    
    private void HandleFailed(string error)
    {
        _pipelineStage.text = $"Failed: {error}";
    }

    private void HandleStageChanged(PipelineStage stage)
    {
       
        _pipelineStage.text = stage switch
        {
            PipelineStage.ExtractingFrames => "Extracting frames from video…",
            PipelineStage.RunningColmap => "Estimating camera positions (COLMAP)…",
            PipelineStage.TrainingBrush => "Training gaussian splat…",
            PipelineStage.Done => "Done!",
            PipelineStage.Cancelled => "Cancelled.",
            PipelineStage.Error => "Something went wrong.",
            _ => _pipelineStage.text
        };

        if (stage is PipelineStage.Done or PipelineStage.Error or PipelineStage.Cancelled)
        {
            // startButton.interactable = true;
            // cancelButton.interactable = false;
        }
    }
}

internal enum InputType
{
    Video,
    Images
}
