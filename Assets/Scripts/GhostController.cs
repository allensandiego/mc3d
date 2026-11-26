using UnityEngine;

public enum GhostType
{
    Blinky,
    Pinky,
    Inky,
    Clyde
}

public enum GhostState
{
    InHouse,
    ExitingHouse,
    Scatter,
    Chase,
    Frightened,
    Eaten
}

public class GhostController : MonoBehaviour
{
    public GhostType ghostType;

    [SerializeField] private float normalSpeed = 4.0f;
    [SerializeField] private float frightenedSpeed = 2.5f;
    [SerializeField] private float eatenSpeed = 9.0f;
    [SerializeField] private float tunnelSpeed = 2.5f;

    public GhostState State { get; private set; }
    public Vector2Int CurrentTile { get; private set; }
    public Vector2Int Direction { get; private set; }

    private Vector2Int targetTile;
    private bool canMove;
    private bool reversePending;
    private float releaseDelay;
    private float stateElapsed;
    private float frightenedDuration;
    private float bobPhase;

    private MeshRenderer bodyRenderer;
    private MeshRenderer[] eyeRenderers;
    private MeshRenderer[] pupilRenderers;
    private Material bodyMaterial;
    private Color baseColor;

    private PacManController pacMan;
    private GhostController blinkyRef;

    private const float GhostZOffset = -0.52f;
    private static readonly Vector3 BoardUp = new Vector3(0f, 0f, -1f);

    private static readonly Vector2Int HouseDoorOutside = new Vector2Int(13, 11);
    private static readonly Vector2Int HouseCenter = new Vector2Int(13, 14);
    private static readonly Vector2Int[] PriorityDirs =
    {
        MazeData.GridUp,
        MazeData.GridLeft,
        MazeData.GridDown,
        MazeData.GridRight
    };

    private void Awake()
    {
        SpriteRenderer oldSr = GetComponent<SpriteRenderer>();
        if (oldSr != null) Destroy(oldSr);

        Build3DGhostModel();
    }

    private void Build3DGhostModel()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        bodyMaterial = new Material(shader);
        if (bodyMaterial.HasProperty("_Glossiness")) bodyMaterial.SetFloat("_Glossiness", 0.55f);
        if (bodyMaterial.HasProperty("_Metallic")) bodyMaterial.SetFloat("_Metallic", 0.1f);

        Material whiteMat = new Material(shader);
        whiteMat.color = Color.white;
        if (whiteMat.HasProperty("_Glossiness")) whiteMat.SetFloat("_Glossiness", 0.6f);

        Material pupilMat = new Material(shader);
        pupilMat.color = new Color(0.06f, 0.12f, 0.45f, 1f);

        // Body
        GameObject bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(transform, false);
        bodyObj.AddComponent<MeshFilter>().sharedMesh = GhostModelMesh.CreateGhostBody(0.43f);
        bodyRenderer = bodyObj.AddComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = bodyMaterial;

        // Eyes & pupils facing +Z (transform.forward)
        MeshRenderer eyeL = CreateSpherePart("Eye_L", transform, new Vector3(-0.15f, 0.18f, 0.35f), new Vector3(0.18f, 0.22f, 0.14f), whiteMat);
        MeshRenderer eyeR = CreateSpherePart("Eye_R", transform, new Vector3(0.15f, 0.18f, 0.35f), new Vector3(0.18f, 0.22f, 0.14f), whiteMat);
        eyeRenderers = new[] { eyeL, eyeR };

        MeshRenderer pupilL = CreateSpherePart("Pupil_L", transform, new Vector3(-0.15f, 0.18f, 0.42f), new Vector3(0.09f, 0.11f, 0.08f), pupilMat);
        MeshRenderer pupilR = CreateSpherePart("Pupil_R", transform, new Vector3(0.15f, 0.18f, 0.42f), new Vector3(0.09f, 0.11f, 0.08f), pupilMat);
        pupilRenderers = new[] { pupilL, pupilR };
    }

    private static MeshRenderer CreateSpherePart(string partName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = partName;
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = localScale;
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return mr;
    }

    public void Initialize(PacManController pac, GhostController blinky)
    {
        pacMan = pac;
        blinkyRef = blinky;
        baseColor = GetTypeColor(ghostType);
        bobPhase = (int)ghostType * 1.3f;
        ResetToSpawn();
    }

    private static Color GetTypeColor(GhostType type)
    {
        switch (type)
        {
            case GhostType.Blinky: return new Color(0.95f, 0.15f, 0.15f, 1f);
            case GhostType.Pinky: return new Color(1.0f, 0.62f, 0.85f, 1f);
            case GhostType.Inky: return new Color(0.12f, 0.92f, 0.95f, 1f);
            case GhostType.Clyde: return new Color(1.0f, 0.58f, 0.12f, 1f);
            default: return Color.white;
        }
    }

    public void ResetToSpawn()
    {
        stateElapsed = 0f;
        reversePending = false;
        canMove = false;

        switch (ghostType)
        {
            case GhostType.Blinky:
                CurrentTile = new Vector2Int(13, 11);
                Direction = MazeData.GridLeft;
                State = GhostState.Scatter;
                releaseDelay = 0f;
                break;
            case GhostType.Pinky:
                CurrentTile = new Vector2Int(13, 14);
                Direction = MazeData.GridUp;
                State = GhostState.InHouse;
                releaseDelay = 0.5f;
                break;
            case GhostType.Inky:
                CurrentTile = new Vector2Int(11, 14);
                Direction = MazeData.GridUp;
                State = GhostState.InHouse;
                releaseDelay = 3.5f;
                break;
            case GhostType.Clyde:
                CurrentTile = new Vector2Int(15, 14);
                Direction = MazeData.GridUp;
                State = GhostState.InHouse;
                releaseDelay = 7.0f;
                break;
        }

        targetTile = State == GhostState.InHouse ? CurrentTile : CurrentTile + Direction;
        transform.position = MazeData.GridToWorld(CurrentTile.x, CurrentTile.y) + new Vector3(0f, 0f, GhostZOffset);
        SetVisible(true);
        UpdateOrientation();
        UpdateAppearance();
    }

    public void SetCanMove(bool value)
    {
        canMove = value;
    }

    public void SetGlobalMode(GhostState globalMode)
    {
        if (State == GhostState.Scatter || State == GhostState.Chase)
        {
            if (State != globalMode)
            {
                State = globalMode;
                reversePending = true;
            }
        }
    }

    public void EnterFrightened(float duration)
    {
        if (State == GhostState.Eaten || State == GhostState.InHouse || State == GhostState.ExitingHouse)
            return;

        frightenedDuration = duration;
        stateElapsed = 0f;
        if (State != GhostState.Frightened)
        {
            State = GhostState.Frightened;
            reversePending = true;
        }
    }

    public void SetEaten()
    {
        State = GhostState.Eaten;
        reversePending = false;
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (bodyRenderer != null) bodyRenderer.enabled = visible;
        if (eyeRenderers != null)
            for (int i = 0; i < eyeRenderers.Length; i++)
                if (eyeRenderers[i] != null) eyeRenderers[i].enabled = visible;
        if (pupilRenderers != null)
            for (int i = 0; i < pupilRenderers.Length; i++)
                if (pupilRenderers[i] != null) pupilRenderers[i].enabled = visible;
    }

    private void Update()
    {
        if (!canMove) return;

        stateElapsed += Time.deltaTime;

        if (State == GhostState.InHouse)
        {
            if (stateElapsed >= releaseDelay)
            {
                State = GhostState.ExitingHouse;
                targetTile = HouseCenter;
                Direction = GetStepToward(HouseCenter, true);
            }
            UpdateOrientation();
            UpdateAppearance();
            return;
        }

        if (State == GhostState.Frightened && stateElapsed >= frightenedDuration)
        {
            State = GhostState.Chase;
        }

        if (reversePending)
        {
            reversePending = false;
            Direction = -Direction;
            Vector2Int swap = CurrentTile;
            CurrentTile = targetTile;
            targetTile = swap;
        }

        float speed = GetCurrentSpeed();
        float remaining = speed * Time.deltaTime;

        while (remaining > 0f)
        {
            float bobZ = GhostZOffset + Mathf.Sin(Time.time * 8f + bobPhase) * 0.04f;
            Vector3 targetWorld = MazeData.GridToWorld(targetTile.x, targetTile.y) + new Vector3(0f, 0f, bobZ);
            Vector3 currentPos = new Vector3(transform.position.x, transform.position.y, bobZ);
            float dist = Vector3.Distance(currentPos, targetWorld);

            if (remaining < dist)
            {
                transform.position = Vector3.MoveTowards(currentPos, targetWorld, remaining);
                remaining = 0f;
            }
            else
            {
                remaining -= dist;
                transform.position = targetWorld;

                if (targetTile.y == MazeData.TunnelRow && (targetTile.x < 0 || targetTile.x >= MazeData.Width))
                {
                    targetTile.x = MazeData.WrapCol(targetTile.x);
                    transform.position = MazeData.GridToWorld(targetTile.x, targetTile.y) + new Vector3(0f, 0f, bobZ);
                }

                CurrentTile = targetTile;
                ChooseNextTile();
            }
        }

        UpdateOrientation();
        UpdateAppearance();
    }

    private void UpdateOrientation()
    {
        Vector3 worldFwd = new Vector3(Direction.x, -Direction.y, 0f);
        if (worldFwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(worldFwd.normalized, BoardUp);
    }

    private float GetCurrentSpeed()
    {
        if (State == GhostState.Eaten) return eatenSpeed;
        if (State == GhostState.Frightened) return frightenedSpeed;
        if (CurrentTile.y == MazeData.TunnelRow && (CurrentTile.x <= 5 || CurrentTile.x >= 22))
            return tunnelSpeed;
        return normalSpeed;
    }

    private void ChooseNextTile()
    {
        if (State == GhostState.ExitingHouse)
        {
            if (CurrentTile == HouseDoorOutside)
            {
                State = GhostState.Chase;
                Direction = MazeData.GridLeft;
                targetTile = CurrentTile + Direction;
                return;
            }
            Direction = GetStepToward(HouseDoorOutside, true);
            targetTile = CurrentTile + Direction;
            return;
        }

        if (State == GhostState.Eaten && CurrentTile == HouseCenter)
        {
            State = GhostState.ExitingHouse;
            Direction = GetStepToward(HouseDoorOutside, true);
            targetTile = CurrentTile + Direction;
            return;
        }

        bool allowGate = State == GhostState.Eaten;

        if (State == GhostState.Frightened)
        {
            Direction = ChooseRandomValidDirection(allowGate);
            targetTile = CurrentTile + Direction;
            return;
        }

        Vector2Int goal = GetTargetTile();
        Direction = ChooseBestDirection(goal, allowGate);
        targetTile = CurrentTile + Direction;
    }

    private Vector2Int GetStepToward(Vector2Int goal, bool allowGate)
    {
        Vector2Int bestDir = MazeData.GridUp;
        float bestDist = float.MaxValue;

        foreach (Vector2Int dir in PriorityDirs)
        {
            Vector2Int next = CurrentTile + dir;
            if (MazeData.IsWall(next.x, next.y, allowGate)) continue;
            float d = (next - goal).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestDir = dir;
            }
        }
        return bestDir;
    }

    private Vector2Int ChooseBestDirection(Vector2Int goal, bool allowGate)
    {
        Vector2Int bestDir = Direction;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (Vector2Int dir in PriorityDirs)
        {
            if (dir == -Direction) continue;
            Vector2Int next = CurrentTile + dir;
            if (MazeData.IsWall(next.x, next.y, allowGate)) continue;

            float d = (next - goal).sqrMagnitude;
            if (!found || d < bestDist)
            {
                bestDist = d;
                bestDir = dir;
                found = true;
            }
        }

        if (!found) bestDir = -Direction;
        return bestDir;
    }

    private Vector2Int ChooseRandomValidDirection(bool allowGate)
    {
        Vector2Int[] valid = new Vector2Int[4];
        int count = 0;

        foreach (Vector2Int dir in PriorityDirs)
        {
            if (dir == -Direction) continue;
            Vector2Int next = CurrentTile + dir;
            if (!MazeData.IsWall(next.x, next.y, allowGate))
            {
                valid[count++] = dir;
            }
        }

        if (count == 0) return -Direction;
        return valid[Random.Range(0, count)];
    }

    private Vector2Int GetTargetTile()
    {
        if (State == GhostState.Eaten)
            return HouseCenter;

        if (State == GhostState.Scatter)
        {
            switch (ghostType)
            {
                case GhostType.Blinky: return new Vector2Int(25, 0);
                case GhostType.Pinky: return new Vector2Int(2, 0);
                case GhostType.Inky: return new Vector2Int(27, 30);
                case GhostType.Clyde: return new Vector2Int(0, 30);
            }
        }

        Vector2Int pacTile = pacMan != null ? pacMan.CurrentTile : new Vector2Int(13, 23);
        Vector2Int pacDir = pacMan != null ? pacMan.Direction : MazeData.GridLeft;

        switch (ghostType)
        {
            case GhostType.Blinky:
                return pacTile;
            case GhostType.Pinky:
                return pacTile + pacDir * 4;
            case GhostType.Inky:
                Vector2Int ahead2 = pacTile + pacDir * 2;
                Vector2Int blinkyTile = blinkyRef != null ? blinkyRef.CurrentTile : pacTile;
                return ahead2 + (ahead2 - blinkyTile);
            case GhostType.Clyde:
                float distSq = (CurrentTile - pacTile).sqrMagnitude;
                return distSq > 64f ? pacTile : new Vector2Int(0, 30);
            default:
                return pacTile;
        }
    }

    private void UpdateAppearance()
    {
        if (bodyRenderer == null || bodyMaterial == null) return;

        bool isEaten = State == GhostState.Eaten;
        bool isFrightened = State == GhostState.Frightened;

        bodyRenderer.enabled = !isEaten;

        if (pupilRenderers != null)
        {
            for (int i = 0; i < pupilRenderers.Length; i++)
                if (pupilRenderers[i] != null) pupilRenderers[i].enabled = !isFrightened;
        }

        if (isFrightened)
        {
            bool flashWhite = (frightenedDuration - stateElapsed) < 2.0f && Mathf.FloorToInt(stateElapsed * 6f) % 2 == 0;
            bodyMaterial.color = flashWhite ? Color.white : new Color(0.15f, 0.25f, 0.95f, 1f);
        }
        else
        {
            bodyMaterial.color = baseColor;
        }
    }
}
