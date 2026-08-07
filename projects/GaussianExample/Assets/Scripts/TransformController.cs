using UnityEngine;
using UnityEngine.UIElements;

public class TransformController : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private GameObject translateGizmoPrefab;
    [SerializeField] private GameObject rotateGizmoPrefab;
    

    private Button _rotateSelector;
    private Button _translateSelector;
    
    private Color _highlightColor = Color.goldenRod;
    private Color _baseColor;

    
    

    public enum TransformSelection
    {
        Translate,
        Rotate,
        Scale,
        None
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var root = _document.rootVisualElement;
        
        _rotateSelector = root.Q<Button>("RotateSelector");
        _rotateSelector.clicked += RotateSelectorClicked;
        _translateSelector = root.Q<Button>("TranslateSelector");
        _translateSelector.clicked += TranslateSelectorClicked;

        _baseColor = new Color(0.737f, 0.737f, 0.737f, 1);
        
        rotateGizmoPrefab.SetActive(false);
        translateGizmoPrefab.SetActive(false);

        SelectionHandoff.SelectedTransform = TransformSelection.None;
    }



    private void RotateSelectorClicked()
    {
        if (SelectionHandoff.SelectedTransform != TransformSelection.Rotate)
        {
            SelectionHandoff.SelectedTransform = TransformSelection.Rotate;
            _rotateSelector.style.backgroundColor = _highlightColor;
            _translateSelector.style.backgroundColor = _baseColor;
            rotateGizmoPrefab.SetActive(true);
            translateGizmoPrefab.SetActive(false);
        }
        else
        {
            SelectionHandoff.SelectedTransform = TransformSelection.None;
            _rotateSelector.style.backgroundColor = _baseColor;
            rotateGizmoPrefab.SetActive(false);
        }
    }

    private void TranslateSelectorClicked()
    {
        if (SelectionHandoff.SelectedTransform != TransformSelection.Translate)
        {
            SelectionHandoff.SelectedTransform = TransformSelection.Translate;
            _translateSelector.style.backgroundColor = _highlightColor;
            _rotateSelector.style.backgroundColor = _baseColor;
            translateGizmoPrefab.SetActive(true);
            rotateGizmoPrefab.SetActive(false);
        }
        else
        {
            SelectionHandoff.SelectedTransform = TransformSelection.None;
            _translateSelector.style.backgroundColor = _baseColor;
            translateGizmoPrefab.SetActive(false);
        }
    }
    
}

public static class SelectionHandoff
{
    public static TransformController.TransformSelection SelectedTransform;
}
