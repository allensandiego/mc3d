using UnityEngine;

/// <summary>
/// Over-the-shoulder third-person camera that sits behind Pac-Man and looks down the corridor he is facing.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    private float backDistance = 2.6f;
    private float shoulderRightOffset = 0.0f;
    private float heightOffset = 1.9f;
    private float lookAheadDistance = 0.5f;
    private float positionSmoothSpeed = 7f;
    private float turnSmoothSpeed = 4.2f;
    private float fieldOfView = 42f;

    private Camera cam;
    private PacManController pacMan;
    private Vector3 smoothForward = new Vector3(-1f, 0f, 0f);
    private Vector3 lastPacPos;
    private bool initialized;

    private static readonly Vector3 BoardUp = new Vector3(0f, 0f, -1f);

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Apply25DView();
    }

    private void LateUpdate()
    {
        if (pacMan == null)
        {
            pacMan = FindAnyObjectByType<PacManController>();
            if (pacMan == null) return;
            SnapToPacMan();
        }

        Vector3 targetForward = GridDirToWorld(pacMan.Direction);
        Vector3 pacPos = pacMan.transform.position;

        // Snap immediately on tunnel wrap or respawn
        if (!initialized || Vector3.Distance(pacPos, lastPacPos) > 5f)
        {
            smoothForward = targetForward;
            SnapToPacMan();
            initialized = true;
            lastPacPos = pacPos;
            return;
        }

        lastPacPos = pacPos;
        smoothForward = Vector3.Slerp(smoothForward, targetForward, 1f - Mathf.Exp(-turnSmoothSpeed * Time.deltaTime)).normalized;

        Vector3 rightVec = Vector3.Cross(BoardUp, smoothForward).normalized;
        Vector3 desiredPos = pacPos - smoothForward * backDistance + rightVec * shoulderRightOffset + BoardUp * heightOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime));

        Vector3 lookTarget = pacPos + smoothForward * lookAheadDistance + BoardUp * 0.1f;
        transform.LookAt(lookTarget, BoardUp);
    }

    public void Apply25DView()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.1f;

        if (pacMan != null)
        {
            SnapToPacMan();
        }
        else
        {
            Vector3 defaultPos = new Vector3(0f, -8f, -0.85f);
            Vector3 fwd = new Vector3(-1f, 0f, 0f);
            Vector3 rightVec = Vector3.Cross(BoardUp, fwd).normalized;
            transform.position = defaultPos - fwd * backDistance + rightVec * shoulderRightOffset + BoardUp * heightOffset;
            transform.LookAt(defaultPos + fwd * lookAheadDistance + BoardUp * 0.1f, BoardUp);
        }
    }

    private void SnapToPacMan()
    {
        if (pacMan == null) return;
        smoothForward = GridDirToWorld(pacMan.Direction);
        Vector3 rightVec = Vector3.Cross(BoardUp, smoothForward).normalized;
        Vector3 pacPos = pacMan.transform.position;
        transform.position = pacPos - smoothForward * backDistance + rightVec * shoulderRightOffset + BoardUp * heightOffset;
        transform.LookAt(pacPos + smoothForward * lookAheadDistance + BoardUp * 0.1f, BoardUp);
    }

    private static Vector3 GridDirToWorld(Vector2Int dir)
    {
        if (dir == Vector2Int.zero) return new Vector3(-1f, 0f, 0f);
        return new Vector3(dir.x, -dir.y, 0f).normalized;
    }
}
