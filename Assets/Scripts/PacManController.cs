using System.Collections;
using UnityEngine;

public class PacManController : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 4.5f;
    [SerializeField] private float chompSpeed = 14f;
    [SerializeField] private float maxMouthAngle = 36f;
    [SerializeField] private float sphereRadius = 0.44f;

    public Vector2Int CurrentTile { get; private set; }
    public Vector2Int Direction { get; private set; }

    private Vector2Int targetTile;
    private bool isMoving;
    private bool canMove;
    private float chompTimer;

    private Transform upperJaw;
    private Transform lowerJaw;
    private MeshRenderer[] bodyRenderers;
    private SwipeInput swipeInput;

    private readonly Vector2Int spawnTile = new Vector2Int(13, 23);
    private const float SphereZOffset = -0.44f;
    private static readonly Vector3 BoardUp = new Vector3(0f, 0f, -1f);

    private void Awake()
    {
        SpriteRenderer oldSr = GetComponent<SpriteRenderer>();
        if (oldSr != null) Destroy(oldSr);

        BuildSphereModel();
    }

    private void BuildSphereModel()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material yellowMat = new Material(shader);
        yellowMat.color = new Color(1.0f, 0.85f, 0.0f, 1.0f);
        if (yellowMat.HasProperty("_Glossiness")) yellowMat.SetFloat("_Glossiness", 0.65f);
        if (yellowMat.HasProperty("_Metallic")) yellowMat.SetFloat("_Metallic", 0.1f);

        Material eyeMat = new Material(shader);
        eyeMat.color = new Color(0.08f, 0.08f, 0.1f, 1.0f);

        // Rotate model root -90 deg around Y so local +X (mouth/eyes) aligns with transform.forward (+Z)
        GameObject modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // Solid back half-sphere (x <= 0)
        GameObject backObj = new GameObject("BackDome");
        backObj.transform.SetParent(modelRoot.transform, false);
        backObj.AddComponent<MeshFilter>().sharedMesh = PacManSphereMesh.CreateBackHalfSphere(sphereRadius);
        MeshRenderer backMr = backObj.AddComponent<MeshRenderer>();
        backMr.sharedMaterial = yellowMat;

        // Upper front jaw (x >= 0, y >= 0)
        GameObject upperObj = new GameObject("UpperJaw");
        upperObj.transform.SetParent(modelRoot.transform, false);
        upperObj.AddComponent<MeshFilter>().sharedMesh = PacManSphereMesh.CreateFrontQuarterJaw(sphereRadius, true);
        MeshRenderer upperMr = upperObj.AddComponent<MeshRenderer>();
        upperMr.sharedMaterial = yellowMat;
        upperJaw = upperObj.transform;

        // Two eyes on the upper hemisphere
        MeshRenderer eyeL = CreateEye("Eye_L", upperJaw, new Vector3(0.20f, 0.26f, 0.20f), eyeMat);
        MeshRenderer eyeR = CreateEye("Eye_R", upperJaw, new Vector3(0.20f, 0.26f, -0.20f), eyeMat);

        // Lower front jaw (x >= 0, y <= 0)
        GameObject lowerObj = new GameObject("LowerJaw");
        lowerObj.transform.SetParent(modelRoot.transform, false);
        lowerObj.AddComponent<MeshFilter>().sharedMesh = PacManSphereMesh.CreateFrontQuarterJaw(sphereRadius, false);
        MeshRenderer lowerMr = lowerObj.AddComponent<MeshRenderer>();
        lowerMr.sharedMaterial = yellowMat;
        lowerJaw = lowerObj.transform;

        bodyRenderers = new[] { backMr, upperMr, lowerMr, eyeL, eyeR };
    }

    private static MeshRenderer CreateEye(string name, Transform parent, Vector3 localPos, Material mat)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = name;
        Collider col = eye.GetComponent<Collider>();
        if (col != null) Destroy(col);
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = new Vector3(0.11f, 0.12f, 0.11f);
        MeshRenderer mr = eye.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return mr;
    }

    public void Initialize(SwipeInput input)
    {
        swipeInput = input;
        if (swipeInput != null) swipeInput.BindPacMan(this);
        ResetToSpawn();
    }

    public void ResetToSpawn()
    {
        StopAllCoroutines();
        transform.localScale = Vector3.one;
        CurrentTile = spawnTile;
        Direction = MazeData.GridLeft;
        targetTile = spawnTile + Direction;
        isMoving = !MazeData.IsWall(targetTile.x, targetTile.y);
        canMove = false;
        chompTimer = 0f;
        transform.position = MazeData.GridToWorld(CurrentTile.x, CurrentTile.y) + new Vector3(0f, 0f, SphereZOffset);
        UpdateOrientation();
        if (swipeInput != null) swipeInput.ClearBufferedDirection();
        SetMouthAngle(maxMouthAngle * 0.4f);
    }

    public void SetCanMove(bool value)
    {
        canMove = value;
    }

    public IEnumerator PlayDeathAnimation()
    {
        canMove = false;
        float duration = 0.85f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetMouthAngle(Mathf.Lerp(25f, 88f, t));
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, t);
            yield return null;
        }
        SetBodyVisible(false);
        transform.localScale = Vector3.one;
    }

    public void Show()
    {
        transform.localScale = Vector3.one;
        SetBodyVisible(true);
    }

    private void SetBodyVisible(bool visible)
    {
        if (bodyRenderers == null) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null) bodyRenderers[i].enabled = visible;
        }
    }

    private void Update()
    {
        if (!canMove) return;

        // Allow immediate 180-degree reversal mid-tile
        Vector2Int buffered = swipeInput != null ? swipeInput.BufferedDirection : Vector2Int.zero;
        if (buffered != Vector2Int.zero && buffered == -Direction)
        {
            Direction = buffered;
            Vector2Int swap = CurrentTile;
            CurrentTile = targetTile;
            targetTile = swap;
            isMoving = !MazeData.IsWall(targetTile.x, targetTile.y);
        }

        if (!isMoving)
        {
            TryStartFromTileCenter(buffered);
            UpdateOrientation();
            UpdateChomp(false);
            return;
        }

        float remainingMove = baseSpeed * Time.deltaTime;
        while (remainingMove > 0f && isMoving)
        {
            Vector3 targetWorld = MazeData.GridToWorld(targetTile.x, targetTile.y) + new Vector3(0f, 0f, SphereZOffset);
            Vector3 pos = transform.position;
            float dist = Vector3.Distance(pos, targetWorld);

            if (remainingMove < dist)
            {
                transform.position = Vector3.MoveTowards(pos, targetWorld, remainingMove);
                remainingMove = 0f;
            }
            else
            {
                remainingMove -= dist;
                transform.position = targetWorld;

                // Handle tunnel wrap
                if (targetTile.y == MazeData.TunnelRow && (targetTile.x < 0 || targetTile.x >= MazeData.Width))
                {
                    targetTile.x = MazeData.WrapCol(targetTile.x);
                    transform.position = MazeData.GridToWorld(targetTile.x, targetTile.y) + new Vector3(0f, 0f, SphereZOffset);
                }

                CurrentTile = targetTile;
                OnReachedTileCenter();
            }
        }

        UpdateOrientation();
        UpdateChomp(isMoving);
    }

    private void UpdateOrientation()
    {
        Vector3 worldFwd = new Vector3(Direction.x, -Direction.y, 0f);
        if (worldFwd.sqrMagnitude < 0.01f) worldFwd = new Vector3(-1f, 0f, 0f);
        transform.rotation = Quaternion.LookRotation(worldFwd.normalized, BoardUp);
    }

    private void TryStartFromTileCenter(Vector2Int buffered)
    {
        if (buffered != Vector2Int.zero && !MazeData.IsWall(CurrentTile.x + buffered.x, CurrentTile.y + buffered.y))
        {
            Direction = buffered;
            targetTile = CurrentTile + Direction;
            isMoving = true;
        }
        else if (!MazeData.IsWall(CurrentTile.x + Direction.x, CurrentTile.y + Direction.y))
        {
            targetTile = CurrentTile + Direction;
            isMoving = true;
        }
    }

    private void OnReachedTileCenter()
    {
        Vector2Int buffered = swipeInput != null ? swipeInput.BufferedDirection : Vector2Int.zero;

        if (buffered != Vector2Int.zero && !MazeData.IsWall(CurrentTile.x + buffered.x, CurrentTile.y + buffered.y))
        {
            Direction = buffered;
            targetTile = CurrentTile + Direction;
            isMoving = true;
            return;
        }

        Vector2Int next = CurrentTile + Direction;
        if (!MazeData.IsWall(next.x, next.y))
        {
            targetTile = next;
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }
    }

    private void UpdateChomp(bool animate)
    {
        if (animate)
        {
            chompTimer += Time.deltaTime * chompSpeed;
            float angle = (Mathf.Sin(chompTimer) * 0.5f + 0.5f) * maxMouthAngle;
            SetMouthAngle(angle);
        }
    }

    private void SetMouthAngle(float degrees)
    {
        if (upperJaw != null) upperJaw.localRotation = Quaternion.Euler(0f, 0f, degrees);
        if (lowerJaw != null) lowerJaw.localRotation = Quaternion.Euler(0f, 0f, -degrees);
    }
}
