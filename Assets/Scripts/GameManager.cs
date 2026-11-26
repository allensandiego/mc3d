using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GamePhase
{
    Ready,
    Playing,
    Dying,
    LevelClear,
    GameOver
}

public class GameManager : MonoBehaviour
{
    private GamePhase phase = GamePhase.Ready;
    private int score;
    private int highScore;
    private int lives = 3;
    private int level = 1;
    private int ghostComboMultiplier = 1;
    private int remainingPellets;

    private float modeTimer;
    private bool inChaseMode;

    private SwipeInput swipeInput;
    private PacManController pacMan;
    private readonly List<GhostController> ghosts = new List<GhostController>();
    private readonly Dictionary<Vector2Int, GameObject> pellets = new Dictionary<Vector2Int, GameObject>();
    private readonly HashSet<Vector2Int> powerPelletTiles = new HashSet<Vector2Int>();
    private readonly List<Renderer> powerPelletRenderers = new List<Renderer>();
    private readonly List<Renderer> wallRenderers = new List<Renderer>();

    private Material wallMaterial;
    private Material pelletMaterial;
    private Transform pelletsParent;
    private Texture2D miniMapTex;

    private void Start()
    {
        highScore = PlayerPrefs.GetInt("MazeChaseHighScore", 0);
        swipeInput = gameObject.AddComponent<SwipeInput>();

        miniMapTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        miniMapTex.SetPixel(0, 0, Color.white);
        miniMapTex.Apply();

        SetupCameraAndLighting();
        BuildWorld();
        SpawnPellets();
        StartCoroutine(ReadySequence(1.8f));
    }

    private void SetupCameraAndLighting()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f, 1f);
            CameraFit fit = cam.GetComponent<CameraFit>();
            if (fit == null) fit = cam.gameObject.AddComponent<CameraFit>();
            fit.Apply25DView();
        }

        if (FindAnyObjectByType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.15f;
            dirLight.color = Color.white;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }

    private static Material CreateLitColorMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    private void BuildWorld()
    {
        // 3D Floor Board
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "MazeFloor";
        floor.transform.position = new Vector3(0f, 0f, 0.15f);
        floor.transform.localScale = new Vector3(28.5f, 31.5f, 0.3f);
        floor.GetComponent<Renderer>().material = CreateLitColorMaterial(new Color(0.03f, 0.03f, 0.08f, 1f));

        // 3D Extruded Maze Walls
        Transform wallsParent = new GameObject("MazeWalls").transform;
        wallMaterial = CreateLitColorMaterial(new Color(0.12f, 0.32f, 1.0f, 1f));
        Material gateMaterial = CreateLitColorMaterial(new Color(1.0f, 0.72f, 1.0f, 1f));
        wallRenderers.Clear();

        for (int row = 0; row < MazeData.Height; row++)
        {
            for (int col = 0; col < MazeData.Width; col++)
            {
                char tile = MazeData.GetTile(col, row);
                if (tile != '#' && tile != '-') continue;

                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(wallsParent);
                Vector3 pos = MazeData.GridToWorld(col, row);

                if (tile == '#')
                {
                    block.name = $"Wall_{col}_{row}";
                    block.transform.position = new Vector3(pos.x, pos.y, -1.6f);
                    block.transform.localScale = new Vector3(0.96f, 0.96f, 3.2f);
                    Renderer r = block.GetComponent<Renderer>();
                    r.material = wallMaterial;
                    wallRenderers.Add(r);
                }
                else
                {
                    block.name = $"Gate_{col}_{row}";
                    block.transform.position = new Vector3(pos.x, pos.y, -0.25f);
                    block.transform.localScale = new Vector3(1.0f, 0.28f, 0.5f);
                    block.GetComponent<Renderer>().material = gateMaterial;
                }
            }
        }

        pelletMaterial = CreateLitColorMaterial(new Color(1.0f, 0.78f, 0.68f, 1f));
        pelletsParent = new GameObject("Pellets").transform;

        // Player (3D chomping sphere)
        GameObject pacObj = new GameObject("PacMan");
        pacMan = pacObj.AddComponent<PacManController>();
        pacMan.Initialize(swipeInput);

        // 4 Enemies
        GhostController blinky = CreateGhost("Blinky", GhostType.Blinky, null);
        CreateGhost("Pinky", GhostType.Pinky, blinky);
        CreateGhost("Inky", GhostType.Inky, blinky);
        CreateGhost("Clyde", GhostType.Clyde, blinky);
    }

    private GhostController CreateGhost(
        string ghostName,
        GhostType type,
        GhostController blinkyRef)
    {
        GameObject obj = new GameObject(ghostName);
        GhostController gc = obj.AddComponent<GhostController>();
        gc.ghostType = type;
        gc.Initialize(pacMan, blinkyRef);
        ghosts.Add(gc);
        return gc;
    }

    private void SpawnPellets()
    {
        foreach (Transform child in pelletsParent)
        {
            Destroy(child.gameObject);
        }
        pellets.Clear();
        powerPelletTiles.Clear();
        powerPelletRenderers.Clear();
        remainingPellets = 0;

        for (int row = 0; row < MazeData.Height; row++)
        {
            for (int col = 0; col < MazeData.Width; col++)
            {
                char tile = MazeData.GetTile(col, row);
                if (tile != '.' && tile != 'O') continue;

                bool isPower = tile == 'O';
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = isPower ? $"PowerPellet_{col}_{row}" : $"Pellet_{col}_{row}";
                p.transform.SetParent(pelletsParent);

                Vector3 pos = MazeData.GridToWorld(col, row);
                p.transform.position = new Vector3(pos.x, pos.y, isPower ? -0.35f : -0.25f);
                float size = isPower ? 0.62f : 0.26f;
                p.transform.localScale = new Vector3(size, size, size);

                Renderer r = p.GetComponent<Renderer>();
                r.material = pelletMaterial;

                Vector2Int key = new Vector2Int(col, row);
                pellets[key] = p;
                remainingPellets++;

                if (isPower)
                {
                    powerPelletTiles.Add(key);
                    powerPelletRenderers.Add(r);
                }
            }
        }
    }

    private void Update()
    {
        // Blink power pellets
        bool blinkOn = Mathf.FloorToInt(Time.time * 5f) % 2 == 0;
        for (int i = 0; i < powerPelletRenderers.Count; i++)
        {
            if (powerPelletRenderers[i] != null)
                powerPelletRenderers[i].enabled = blinkOn;
        }

        if (phase == GamePhase.GameOver)
        {
            if (swipeInput.TapOrActionPressed)
            {
                RestartFullGame();
            }
            return;
        }

        if (phase != GamePhase.Playing) return;

        UpdateScatterChaseCycle();
        CheckPelletPickup();
        CheckGhostCollisions();
    }

    private void UpdateScatterChaseCycle()
    {
        modeTimer += Time.deltaTime;
        float limit = inChaseMode ? 20f : 7f;
        if (modeTimer >= limit)
        {
            modeTimer = 0f;
            inChaseMode = !inChaseMode;
            GhostState nextMode = inChaseMode ? GhostState.Chase : GhostState.Scatter;
            for (int i = 0; i < ghosts.Count; i++)
                ghosts[i].SetGlobalMode(nextMode);
        }
    }

    private void CheckPelletPickup()
    {
        Vector2Int tile = pacMan.CurrentTile;
        if (!pellets.TryGetValue(tile, out GameObject pelletObj)) return;

        pellets.Remove(tile);
        Destroy(pelletObj);
        remainingPellets--;

        if (powerPelletTiles.Contains(tile))
        {
            AddScore(50);
            ghostComboMultiplier = 1;
            float frightTime = Mathf.Max(2.5f, 7.0f - (level - 1) * 0.7f);
            for (int i = 0; i < ghosts.Count; i++)
                ghosts[i].EnterFrightened(frightTime);
        }
        else
        {
            AddScore(10);
        }

        if (remainingPellets <= 0)
        {
            StartCoroutine(LevelClearSequence());
        }
    }

    private void CheckGhostCollisions()
    {
        Vector2 pacPos = new Vector2(pacMan.transform.position.x, pacMan.transform.position.y);
        for (int i = 0; i < ghosts.Count; i++)
        {
            GhostController g = ghosts[i];
            if (g.State == GhostState.Eaten || g.State == GhostState.InHouse) continue;

            Vector2 ghostPos = new Vector2(g.transform.position.x, g.transform.position.y);
            bool sameTile = g.CurrentTile == pacMan.CurrentTile;
            if (sameTile || Vector2.Distance(pacPos, ghostPos) < 0.85f)
            {
                if (g.State == GhostState.Frightened)
                {
                    g.SetEaten();
                    AddScore(200 * ghostComboMultiplier);
                    ghostComboMultiplier = Mathf.Min(8, ghostComboMultiplier * 2);
                }
                else
                {
                    StartCoroutine(DeathSequence());
                    break;
                }
            }
        }
    }

    private void AddScore(int points)
    {
        score += points;
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("MazeChaseHighScore", highScore);
        }
    }

    private IEnumerator ReadySequence(float duration)
    {
        phase = GamePhase.Ready;
        pacMan.Show();
        pacMan.ResetToSpawn();
        for (int i = 0; i < ghosts.Count; i++)
            ghosts[i].ResetToSpawn();

        yield return new WaitForSeconds(duration);

        phase = GamePhase.Playing;
        modeTimer = 0f;
        inChaseMode = false;
        pacMan.SetCanMove(true);
        for (int i = 0; i < ghosts.Count; i++)
            ghosts[i].SetCanMove(true);
    }

    private IEnumerator DeathSequence()
    {
        phase = GamePhase.Dying;
        pacMan.SetCanMove(false);
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].SetCanMove(false);
            ghosts[i].Hide();
        }

        yield return pacMan.PlayDeathAnimation();
        yield return new WaitForSeconds(0.5f);

        lives--;
        if (lives <= 0)
        {
            phase = GamePhase.GameOver;
        }
        else
        {
            StartCoroutine(ReadySequence(1.4f));
        }
    }

    private IEnumerator LevelClearSequence()
    {
        phase = GamePhase.LevelClear;
        pacMan.SetCanMove(false);
        for (int i = 0; i < ghosts.Count; i++)
            ghosts[i].SetCanMove(false);

        Color baseBlue = new Color(0.12f, 0.32f, 1.0f, 1f);
        for (int flash = 0; flash < 6; flash++)
        {
            if (wallMaterial != null)
                wallMaterial.color = flash % 2 == 0 ? Color.white : baseBlue;
            yield return new WaitForSeconds(0.2f);
        }
        if (wallMaterial != null) wallMaterial.color = baseBlue;

        level++;
        SpawnPellets();
        StartCoroutine(ReadySequence(1.6f));
    }

    private void RestartFullGame()
    {
        score = 0;
        lives = 3;
        level = 1;
        SpawnPellets();
        StartCoroutine(ReadySequence(1.5f));
    }

    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 450f, Screen.height / 800f);
        if (scale <= 0f) scale = 1f;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        float virtualW = Screen.width / scale;
        float virtualH = Screen.height / scale;

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        headerStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 12, virtualW * 0.45f, 60), $"SCORE\n{score}", headerStyle);
        GUI.Label(new Rect(virtualW * 0.55f - 10, 12, virtualW * 0.45f, 60), $"HIGH SCORE\n{highScore}", headerStyle);

        GUIStyle footerStyle = new GUIStyle(headerStyle)
        {
            fontSize = 18,
            alignment = TextAnchor.LowerLeft
        };
        GUI.Label(new Rect(20, virtualH - 42, virtualW - 280, 32), $"LIVES: {lives}    LEVEL: {level}", footerStyle);

        DrawMiniMap(virtualW, virtualH);

        if (phase == GamePhase.Ready)
        {
            GUIStyle readyStyle = new GUIStyle(headerStyle) { fontSize = 26 };
            readyStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, virtualH * 0.52f, virtualW, 40), "READY!", readyStyle);
        }
        else if (phase == GamePhase.GameOver)
        {
            GUIStyle overStyle = new GUIStyle(headerStyle) { fontSize = 26 };
            overStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(0, virtualH * 0.48f, virtualW, 80), "GAME OVER\nTAP TO RESTART", overStyle);
        }

        GUI.matrix = prevMatrix;
    }

    private void DrawMiniMap(float virtualW, float virtualH)
    {
        if (miniMapTex == null) return;

        const float cell = 8.4f;
        float mapW = MazeData.Width * cell;
        float mapH = MazeData.Height * cell;
        float pad = 6f;
        float originX = virtualW - mapW - 16f;
        float originY = virtualH - mapH - 16f;

        Color prevColor = GUI.color;

        // Border & background
        GUI.color = new Color(0.12f, 0.32f, 1.0f, 0.9f);
        GUI.DrawTexture(new Rect(originX - pad - 2f, originY - pad - 2f, mapW + (pad + 2f) * 2f, mapH + (pad + 2f) * 2f), miniMapTex);

        GUI.color = new Color(0.02f, 0.02f, 0.06f, 0.88f);
        GUI.DrawTexture(new Rect(originX - pad, originY - pad, mapW + pad * 2f, mapH + pad * 2f), miniMapTex);

        // Walls & gate
        for (int row = 0; row < MazeData.Height; row++)
        {
            for (int col = 0; col < MazeData.Width; col++)
            {
                char t = MazeData.GetTile(col, row);
                if (t == '#')
                {
                    GUI.color = new Color(0.15f, 0.38f, 1.0f, 0.95f);
                    GUI.DrawTexture(new Rect(originX + col * cell, originY + row * cell, cell, cell), miniMapTex);
                }
                else if (t == '-')
                {
                    GUI.color = new Color(1.0f, 0.72f, 1.0f, 0.95f);
                    GUI.DrawTexture(new Rect(originX + col * cell, originY + row * cell + cell * 0.3f, cell, cell * 0.4f), miniMapTex);
                }
            }
        }

        // Remaining pellets
        bool blinkOn = Mathf.FloorToInt(Time.time * 5f) % 2 == 0;
        GUI.color = new Color(1.0f, 0.78f, 0.68f, 0.9f);
        foreach (KeyValuePair<Vector2Int, GameObject> kvp in pellets)
        {
            Vector2Int p = kvp.Key;
            bool isPower = powerPelletTiles.Contains(p);
            if (isPower && !blinkOn) continue;
            float size = isPower ? cell * 0.75f : cell * 0.35f;
            float offset = (cell - size) * 0.5f;
            GUI.DrawTexture(new Rect(originX + p.x * cell + offset, originY + p.y * cell + offset, size, size), miniMapTex);
        }

        // Ghosts
        for (int i = 0; i < ghosts.Count; i++)
        {
            GhostController g = ghosts[i];
            if (g == null) continue;
            Vector2Int gt = g.CurrentTile;
            GUI.color = GetGhostMiniMapColor(g);
            GUI.DrawTexture(new Rect(originX + gt.x * cell - 0.5f, originY + gt.y * cell - 0.5f, cell + 1f, cell + 1f), miniMapTex);
        }

        // Pac-Man
        if (pacMan != null)
        {
            Vector2Int pt = pacMan.CurrentTile;
            GUI.color = Color.yellow;
            GUI.DrawTexture(new Rect(originX + pt.x * cell - 0.5f, originY + pt.y * cell - 0.5f, cell + 1f, cell + 1f), miniMapTex);
        }

        GUI.color = prevColor;
    }

    private static Color GetGhostMiniMapColor(GhostController g)
    {
        if (g.State == GhostState.Frightened) return new Color(0.2f, 0.4f, 1.0f, 1f);
        if (g.State == GhostState.Eaten) return new Color(0.85f, 0.85f, 0.9f, 0.9f);

        switch (g.ghostType)
        {
            case GhostType.Blinky: return new Color(1.0f, 0.15f, 0.15f, 1f);
            case GhostType.Pinky: return new Color(1.0f, 0.65f, 0.85f, 1f);
            case GhostType.Inky: return new Color(0.15f, 0.95f, 1.0f, 1f);
            case GhostType.Clyde: return new Color(1.0f, 0.65f, 0.15f, 1f);
            default: return Color.white;
        }
    }
}
