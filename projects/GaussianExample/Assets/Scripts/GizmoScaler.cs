
using UnityEngine;


public class GizmoScaler : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private GameObject translateGizmo;
    [SerializeField] private GameObject rotateGizmo;

    private Transform[] gizmoRoots;
    private Vector3[] startScales;
    private float[] startDistances;
    private Vector3 cameraPosition;

    void Start()
    {
     
        gizmoRoots = new []{translateGizmo.transform, rotateGizmo.transform};

        cameraPosition = _camera.transform.position;

        startScales = new Vector3[gizmoRoots.Length];
        startDistances = new float[gizmoRoots.Length];

        for (int i = 0; i < gizmoRoots.Length; i++)
        {
            startScales[i] = gizmoRoots[i].localScale;
            startDistances[i] = Vector3.Distance(cameraPosition, gizmoRoots[i].position);
        }
    }

    void Update()
    {
        Vector3 newPosition = _camera.transform.position;
        
        for (int i = 0; i < gizmoRoots.Length; i++)
        {
            float currentDistance = Vector3.Distance(newPosition, gizmoRoots[i].position);
            if (currentDistance != startDistances[i] || newPosition != cameraPosition)
            {
                float ratio = currentDistance / startDistances[i];
                gizmoRoots[i].localScale = startScales[i] * ratio;
            }
        }

        cameraPosition = newPosition;
        
    }
}
