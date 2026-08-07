using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms.VisualStyles;
using GaussianSplatting.Runtime;
using SFB;
using SplattingPipeline;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;
using Button = UnityEngine.UIElements.Button;


public class UIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDoc;
    [SerializeField] private GaussianSplatRenderer splatRendererPrefab;
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
    private Button _importButton;
    private Button _selectOutputButton;
    private Foldout _settingsFoldout;
    private EnumField _presetDropdown;
    private DropdownField _resolutionDropdown;
    private SliderInt _iterationsSlider;
    private SliderInt _framerateSlider;
    private ProgressBar _progressBar;
    private Color _baseTextColor = Color.white;

    private PipelineSettings _pipelineSettings;
    [SerializeField]
    private Camera _cam;
    
    private enum PipelinePreset
    {
        None,Fast, Standard, High, UltraHigh
    }

    void Awake()
    {
        _pipelineSettings = new PipelineSettings();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.gray2;
        
        root = uiDoc.GetComponent<UIDocument>().rootVisualElement;
        
        // logConsole = root.Q<Label>("LogConsole");

        _selectButton = root.Q<Button>("InputSelection");
        _selectButton.clickable.clicked += InputSelectionButtonClicked;
        _selectOutputButton = root.Q<Button>("OutputSelection");
        _selectOutputButton.clicked += SelectOutputButtonClicked;
        _startButton = root.Q<Button>("StartButton");
        _startButton.clicked += StartButtonClicked;
        _importButton =  root.Q<Button>("ImportButton");
        _importButton.clicked += ImportButtonClicked;
        _cancelButton = root.Q<Button>("CancelButton");
        _cancelButton.clicked += CancelButtonClicked;
        _cancelButton.style.display =  DisplayStyle.None;
        
        _inputLabel =  root.Q<Label>("InputLabel");
        _outputLabel = root.Q<Label>("OutputLabel");
        _pipelineStage = root.Q<Label>("PipelineStage");
        _pipelineStage.style.display = DisplayStyle.None;
        
        _settingsFoldout = root.Q<Foldout>("SettingsFoldout");
        
        _presetDropdown = root.Q<EnumField>("PresetDropdown");
        _presetDropdown.Init(PipelinePreset.None);
        _presetDropdown.value = PipelinePreset.Standard; 
        
        _presetDropdown.RegisterCallback<ChangeEvent<Enum>>((evt) =>
        {
            switch (evt.newValue)
            {
                case PipelinePreset.None :
                {
                    _resolutionDropdown.SetEnabled(true);
                    _iterationsSlider.SetEnabled(true);
                }; break;
                case PipelinePreset.Standard:
                {
                    _resolutionDropdown.SetEnabled(false);
                    _iterationsSlider.SetEnabled(false);
                    _pipelineSettings = new PipelineSettings(maxResolution: 1024, numIterations: 20000);
                }; break;
                case PipelinePreset.Fast : 
                {
                    _resolutionDropdown.SetEnabled(false);
                    _iterationsSlider.SetEnabled(false);
                    _pipelineSettings = new PipelineSettings(maxResolution: 512, numIterations: 3000);
        
                } ; break;
                case PipelinePreset.High :  
                {
                    _resolutionDropdown.SetEnabled(false);
                    _iterationsSlider.SetEnabled(false);
                    _pipelineSettings = new PipelineSettings(maxResolution: 1280, numIterations: 30000);
        
                }; break;
                case PipelinePreset.UltraHigh : 
                {
                    _resolutionDropdown.SetEnabled(false);
                    _iterationsSlider.SetEnabled(false);
                    _pipelineSettings = new PipelineSettings(maxResolution: 1920, numIterations: 50000);
        
                }; break;
            }
        });
       
        var choices = new List<string> {"512", "768", "1208", "1920", "2048"};
        _resolutionDropdown =  root.Q<DropdownField>("ResolutionDropdown");
        _resolutionDropdown.choices = choices;
        _resolutionDropdown.value = choices[0];
        _resolutionDropdown.RegisterCallback<ChangeEvent<string>>((evt) =>
        {
            _pipelineSettings.MaxResolution = int.Parse(evt.newValue);
        });
        
        _iterationsSlider = root.Q<SliderInt>("IterationSlider");
        _iterationsSlider.RegisterCallback<ChangeEvent<int>>((evt) =>
        {
            _pipelineSettings.NumIterations = evt.newValue;
        });
        _framerateSlider = root.Q<SliderInt>("FramerateSlider");
        _framerateSlider.RegisterCallback<ChangeEvent<int>>((evt) =>
        {
            _pipelineSettings.ExtractionFramerate = evt.newValue;
        });
        
        _resolutionDropdown.SetEnabled(false);
        _iterationsSlider.SetEnabled(false);

        
        _progressBar = root.Q<ProgressBar>("StageProgressBar");
        _progressBar.lowValue = 0f;
        _progressBar.highValue = 1f;
        _progressBar.style.display = DisplayStyle.None;
        
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
    
    

    private void InputSelectionButtonClicked()
    {
        
        var extensions = new[] {
            new ExtensionFilter("Media Files", "png", "jpg", "jpeg", "mp4", "mov", "avi", "mkv", "webm"),
        };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Images or Video", "", extensions, true);
        if (Validate( out var error, paths))
        {
            _inputLabel.style.color = _baseTextColor;
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

    private void ImportButtonClicked()
    {
        var extensions = new[] {
            new ExtensionFilter("Splat Files", "ply"),
        };
        string[] importFile = StandaloneFileBrowser.OpenFilePanel("Select Splat to Import", "", extensions, false);
        if (importFile.Length > 0)
            HandleFinished(importFile[0]);
    }

    private void SelectOutputButtonClicked()
    {
        string[] outputDirectory = StandaloneFileBrowser.OpenFolderPanel("Select Output Directory", "",false);
        if (outputDirectory.Length <= 0)
        {
            _outputLabel.style.color = Color.red;
            _outputLabel.text = "Please select a directory";
            return;
        }
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
        
    
        if (!isProcessRunning)
        {
            isProcessRunning = true;
        }
        else
        {
            return;
        }

        _selectButton.style.display = DisplayStyle.None;
        _selectOutputButton.style.display = DisplayStyle.None;
        _settingsFoldout.style.display = DisplayStyle.None;
        _startButton.style.display = DisplayStyle.None;
        _importButton.style.display = DisplayStyle.None;
        _inputLabel.style.display = DisplayStyle.None;
        _outputLabel.style.display = DisplayStyle.None;

        _cancelButton.style.display = DisplayStyle.Flex;
        _pipelineStage.style.display = DisplayStyle.Flex;
        _progressBar.style.display = DisplayStyle.Flex;
        
        // string workDir = Path.Combine(Application.persistentDataPath, "jobs", System.Guid.NewGuid().ToString());
        string workDir = Path.Combine(_selectedOutputPath, "jobs", System.Guid.NewGuid().ToString());
        // _pipelineStage.text = workDir;
        await _pipeline.RunAsync(_selectedInputPath, workDir, _selectedInputType, _pipelineSettings);
        // await _pipeline.RunAsync(_selectedInputPath,_selectedOutputPath);
    }

    private void CancelButtonClicked()
    {
        if (!isProcessRunning) return;
        _pipeline.Cancel();
        isProcessRunning = false;
        _selectButton.style.display = DisplayStyle.Flex;
        _selectOutputButton.style.display = DisplayStyle.Flex;
        _settingsFoldout.style.display = DisplayStyle.Flex;
        _startButton.style.display = DisplayStyle.Flex;
        _importButton.style.display = DisplayStyle.Flex;
        _inputLabel.style.display = DisplayStyle.Flex;
        _outputLabel.style.display = DisplayStyle.Flex;

        _cancelButton.style.display = DisplayStyle.None;
        _progressBar.style.display = DisplayStyle.None;


    }
    
    
    
    private void HandleFinished(string splatPath)
    {
        _pipelineStage.text = $"Splat ready: {splatPath}";

        GaussianSplatAsset asset;
        try
        {
            asset = GaussianSplatRuntimeImporter.Import(
                splatPath,
                GaussianSplatRuntimeImporter.DataQuality.Medium,
                importCameras: true,
                onProgress: (stage, pct) => _pipelineStage.text = $"{stage} ({pct:P0})");
        }
        catch (Exception ex)
        {
            _pipelineStage.text = $"Import failed: {ex.Message}";
            Debug.LogException(ex);
            return;
        }

        SplatHandoff.PendingAsset = asset;
        SplatHandoff.FilePath = splatPath;
        SceneManager.LoadSceneAsync("Gaussian Renderer", LoadSceneMode.Single).completed += OnViewerSceneLoaded;
    }

 
    private void OnViewerSceneLoaded(AsyncOperation op)
    {
        var asset = SplatHandoff.PendingAsset;

        if (asset == null)
        {
            Debug.LogError("No splat asset was handed off to the viewer scene.");
            return;
        }

        if (splatRendererPrefab == null)
        {
            Debug.LogError("Splat renderer prefab is not assigned.");
            return;
        }

        var renderer = Instantiate(splatRendererPrefab);
        renderer.gameObject.name = $"Splat_{asset.name}";
        renderer.m_Asset = asset;
        
        var gizmos = FindObjectsByType<GizmoController>(FindObjectsInactive.Include, 
            FindObjectsSortMode.None);
        foreach (var gizmo in gizmos)
        {
            gizmo.target = renderer.transform;
        }
    }
    
    public static class SplatHandoff
    {
        public static GaussianSplatAsset PendingAsset;
        public static string FilePath;
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

        
    }
}

public enum InputType
{
    Video,
    Images
}
