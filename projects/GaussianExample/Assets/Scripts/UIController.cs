using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDoc;
    private VisualElement root;
    private bool isInputSelected = false;
    private bool isProcessRunning = false;
    
    [Header("Bundled Tool Paths (relative to StreamingAssets)")]
    public string colmapRelativePath = "tools/colmap/COLMAP.bat";
    public string ffmpegRelativePath = "tools/ffmpeg/ffmpeg.exe";
    public string brushRelativePath = "Tools/brush-app-x86_64-pc-windows-msvc/brush_app.exe";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        root = uiDoc.GetComponent<UIDocument>().rootVisualElement;
        var inputSelectionButton = root.Q<Button>("InputSelection");
        inputSelectionButton.clickable.clicked += InputSelectionButtonClicked;
        var startButton = root.Q<Button>("StartButton");
        startButton.clicked += StartButtonClicked;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    private void InputSelectionButtonClicked(){}

    private void StartButtonClicked()
    {
        if (!isInputSelected)
        {
            //todo add no input message
            return;
        }
        if (!isProcessRunning) return;
    }
}
