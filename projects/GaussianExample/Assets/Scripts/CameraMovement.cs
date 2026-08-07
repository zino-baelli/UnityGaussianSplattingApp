using SplattingPipeline;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;              // Point the camera orbits/zooms around
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 20f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 200f;    // Degrees per second per unit mouse delta
    [SerializeField] private float minPitch = -80f;         // Prevent flipping over the top/bottom
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY = false;

    private float currentDistance;
    private float yaw;   // Rotation around Y axis (horizontal)
    private float pitch; // Rotation around X axis (vertical)

    private void Start()
    {
        // Initialize spherical coordinates from the camera's current position/rotation
        Vector3 targetPos = GetTargetPosition();
        Vector3 offset = transform.position - targetPos;

        currentDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);

        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;

        // Normalize pitch into -180..180 range for clamping later
        if (pitch > 180f) pitch -= 360f;
    }

    private void Update()
    {
        HandleZoom();
        HandleRotation();
    }

    private void LateUpdate()
    {
        ApplyTransform();
    }

    private void HandleZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0f)
        {
            currentDistance -= scrollInput * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }
    }

    private void HandleRotation()
    {
        // Middle mouse button held down
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw += mouseX * (rotationSpeed * MultiplierHandoff.Multiplier) * Time.deltaTime;

            float pitchDelta = mouseY * (rotationSpeed * MultiplierHandoff.Multiplier) * Time.deltaTime;
            pitch += invertY ? pitchDelta : -pitchDelta;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private void ApplyTransform()
    {
        Vector3 targetPos = GetTargetPosition();

        // Convert spherical coordinates (yaw, pitch, distance) into a position offset
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;
        Vector3 desiredPosition = targetPos + direction * currentDistance;

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }

    private Vector3 GetTargetPosition()
    {
        return target != null ? target.position + targetOffset : targetOffset;
    }
}
