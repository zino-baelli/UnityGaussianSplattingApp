using UnityEngine;

public class GizmoController : MonoBehaviour
{
    public Transform target;           
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;
    public Camera cam;

  
    public Material xMat;
    public Material zMat;
    public Material yMat;
    public Material activeMat;

    // public float rotateSpeed = 200f;
    // public float scaleSpeed = 1f;
    public LayerMask gizmoLayerMask;

    private Transform selectedAxis;
    private Transform hoveredAxis;
    private Vector3 dragStartMouse;
    private Vector3 dragStartTargetPos;
    private Vector3 dragStartTargetScale;
    private Quaternion dragStartTargetRot;
    private Plane dragPlane;
    private bool isDragging;
    
    private float dragStartAngle;
    private Vector3 rotatePlaneRight;
    private Vector3 rotatePlaneUp;

    private SelectedAxis _selectedAxis;

    private enum SelectedAxis
    {
        X,
        Y,
        Z
    }
        
    void Awake()
    {
        gizmoLayerMask = LayerMask.GetMask("Gizmo");
    }

    void Update()
    {
        if (target == null || cam == null) return;

        // Keep gizmo at target's position/rotation
        transform.position = target.position;
        // transform.rotation = target.rotation;

      
        if (!isDragging)
        {
            HandleHover();
            if (Input.GetMouseButtonDown(0) && hoveredAxis != null)
            {
                BeginDrag(hoveredAxis);
            }
        }
        else
        {
            if (Input.GetMouseButton(0))
            {
                ContinueDrag();
            }
            else
            {
                EndDrag();
            }
        }
    }

    void HandleHover()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
    
        Transform newHover = null;
        if (Physics.Raycast(ray, out hit, 1000f, gizmoLayerMask))
        {
            if (hit.transform == xAxis || hit.transform == yAxis || hit.transform == zAxis)
                newHover = hit.transform;
        }
    
        if (newHover != hoveredAxis)
        {
            hoveredAxis = newHover;
        }
    }

    void BeginDrag(Transform axis)
    {
        if (axis == xAxis) _selectedAxis = SelectedAxis.X;
        else if (axis == yAxis) _selectedAxis = SelectedAxis.Y;
        else if (axis == zAxis) _selectedAxis = SelectedAxis.Z;
        
       
        isDragging = true;
        selectedAxis = axis;
        SetAxisMaterial(selectedAxis, activeMat);

        dragStartMouse = Input.mousePosition;
        dragStartTargetPos = target.position;
        dragStartTargetRot = target.rotation;
        dragStartTargetScale = target.localScale;

        Vector3 axisDir = GetAxisDirection(selectedAxis);

        if (SelectionHandoff.SelectedTransform == TransformController.TransformSelection.Rotate)
        {
            // The ring lies on a plane whose normal IS the rotation axis.
            dragPlane = new Plane(axisDir, target.position);

            // Build two perpendicular reference directions on that plane,
            // used to measure angles consistently frame to frame.
            rotatePlaneRight = Vector3.Cross(axisDir, Vector3.up);
            if (rotatePlaneRight.sqrMagnitude < 0.0001f)
                rotatePlaneRight = Vector3.Cross(axisDir, Vector3.forward);
            rotatePlaneRight.Normalize();
            rotatePlaneUp = Vector3.Cross(axisDir, rotatePlaneRight);

            dragStartAngle = GetAngleOnPlane(dragStartMouse, axisDir);
        }
        else
        {
            // Move/Scale still use a screen-facing plane
            Vector3 planeNormal = Vector3.Cross(axisDir, cam.transform.right);
            if (planeNormal.sqrMagnitude < 0.0001f)
                planeNormal = Vector3.Cross(axisDir, cam.transform.up);

            dragPlane = new Plane(planeNormal, target.position);
        }
    }

    void ContinueDrag()
    {
        Vector3 axisDir = GetAxisDirection(selectedAxis);
        
        if (SelectionHandoff.SelectedTransform == TransformController.TransformSelection.Rotate)
        {
            float currentAngle = GetAngleOnPlane(Input.mousePosition, axisDir);

            // Shortest signed difference, handles the 359->0 wraparound correctly
            float deltaAngle = Mathf.DeltaAngle(dragStartAngle, currentAngle);

            target.rotation = Quaternion.AngleAxis(deltaAngle, axisDir) * dragStartTargetRot;
            return;
        }
        
        
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        float enter;
        if (!dragPlane.Raycast(ray, out enter)) return;
        Vector3 currentPoint = ray.GetPoint(enter);

        Ray startRay = cam.ScreenPointToRay(dragStartMouse);
        float startEnter;
        if (!dragPlane.Raycast(startRay, out startEnter)) return;
        Vector3 startPoint = startRay.GetPoint(startEnter);

        Vector3 worldDelta = currentPoint - startPoint;
        float amountAlongAxis = Vector3.Dot(worldDelta, axisDir);

        switch (SelectionHandoff.SelectedTransform)
        {
            
            case TransformController.TransformSelection.Translate:
                target.position = dragStartTargetPos + axisDir * amountAlongAxis;
                break;

            // case TransformController.TransformSelection.Rotate:
            //     float angle = amountAlongAxis * rotateSpeed;
            //     target.rotation = Quaternion.AngleAxis(angle, axisDir) * dragStartTargetRot;
            //     break;

            // case TransformController.TransformSelection.Scale:
            //     Vector3 scaleDelta = new Vector3(
            //         Mathf.Abs(axisDir.x) > 0.5f ? amountAlongAxis * scaleSpeed : 0f,
            //         Mathf.Abs(axisDir.y) > 0.5f ? amountAlongAxis * scaleSpeed : 0f,
            //         Mathf.Abs(axisDir.z) > 0.5f ? amountAlongAxis * scaleSpeed : 0f
            //     );
            //     target.localScale = dragStartTargetScale + scaleDelta;
            //     break;
        }
    }
    
    /// <summary>
    /// Projects a screen point onto the rotation plane and returns its angle
    /// (in degrees) relative to the plane's reference axes.
    /// </summary>
    float GetAngleOnPlane(Vector3 screenPoint, Vector3 axisDir)
    {
        Ray ray = cam.ScreenPointToRay(screenPoint);
        float enter;
        if (!dragPlane.Raycast(ray, out enter)) return 0f;

        Vector3 pointOnPlane = ray.GetPoint(enter);
        Vector3 localDir = pointOnPlane - target.position;

        float x = Vector3.Dot(localDir, rotatePlaneRight);
        float y = Vector3.Dot(localDir, rotatePlaneUp);

        return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
    }

    void EndDrag()
    {
        isDragging = false;

        Material defaultMat = activeMat;
        switch (_selectedAxis)
        {
            case SelectedAxis.X: defaultMat = xMat; break;
            case SelectedAxis.Y: defaultMat =  yMat; break;
            case SelectedAxis.Z: defaultMat = zMat; break;
        }
        
        SetAxisMaterial(selectedAxis, defaultMat);
        selectedAxis = null;
    }

    Vector3 GetAxisDirection(Transform axis)
    {
        if (axis == xAxis) return transform.right;
        if (axis == yAxis) return transform.up;
        if (axis == zAxis) return transform.forward;
        return Vector3.zero;
    }

    void SetAxisMaterial(Transform axis, Material mat)
    {
        if (axis == null || mat == null) return;
        Renderer rend = axis.GetComponent<Renderer>();
        if (rend != null) rend.material = mat;
    }

}
