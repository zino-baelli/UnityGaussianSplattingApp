using System.IO;
using SFB;
using SplattingPipeline;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ViewerUIController : MonoBehaviour
{
    [SerializeField]
    private UIDocument _document;
    [SerializeField]
    private Camera _cam;
    
    private Slider _backgroundSlider;
    private SliderInt _cameraSpeedSlider;
    private Button _newButton;
    private Button _exportButton;
    private float _backgroundColor;
    
    
    void Awake()
    {
        var root = _document.rootVisualElement;
        
        
        _newButton = root.Q<Button>("NewButton");
        _newButton.clicked += NewButtonClicked;
        _exportButton = root.Q<Button>("ExportButton");
        _exportButton.clicked += ExportButtonClicked;
        
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        
       
        _backgroundSlider = root.Q<Slider>("BackgroundSlider");
        _backgroundSlider.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            _backgroundColor = Mathf.Round(evt.newValue / 0.01f) * 0.01f;
            _cam.backgroundColor = new Color(_backgroundColor, _backgroundColor, _backgroundColor, 1);
        });
        
        _cameraSpeedSlider = root.Q<SliderInt>("CameraSpeed");
        _cameraSpeedSlider.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            MultiplierHandoff.Multiplier = evt.newValue;
        });
    }
    

    void NewButtonClicked()
    {
        SceneManager.LoadScene("Menu");
    }

    void ExportButtonClicked()
    {
        var path = UIController.SplatHandoff.FilePath;
        var extensions = new[]
        {
            new ExtensionFilter("Splat File", "ply")
        };
        var destination = StandaloneFileBrowser.SaveFilePanel("Save File","",$"splat_{Path.GetFileNameWithoutExtension(path)}",extensions);
        if (destination is { Length: > 0 })
            File.Copy(path, destination, false);
    }
}
