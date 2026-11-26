using UnityEngine;

public static class MazeData
{
    public const int Width = 28;
    public const int Height = 31;
    public const int TunnelRow = 14;

    public static readonly Vector2Int GridUp = new Vector2Int(0, -1);
    public static readonly Vector2Int GridDown = new Vector2Int(0, 1);
    public static readonly Vector2Int GridLeft = new Vector2Int(-1, 0);
    public static readonly Vector2Int GridRight = new Vector2Int(1, 0);

    // Original symmetric 28x31 maze layout:
    // '#' = wall, '.' = pellet, 'O' = power pellet, '-' = enemy house gate, ' ' = empty path
    public static readonly string[] Layout = new string[]
    {
        "############################", // 0
        "#............##............#", // 1
        "#.###.######.##.######.###.#", // 2
        "#O# #......#....#......# #O#", // 3
        "#.###.####.######.####.###.#", // 4
        "#..........................#", // 5
        "#.####.##.########.##.####.#", // 6
        "#......##....##....##......#", // 7
        "######.#####.##.#####.######", // 8
        "     #.##..........##.#     ", // 9
        "     #.##.########.##.#     ", // 10
        "     #.##          ##.#     ", // 11
        "######.## ###--### ##.######", // 12
        "      ... #      # ...      ", // 13
        "      .## #      # ##.      ", // 14 (tunnel)
        "######.## #      # ##.######", // 15
        "     #.## ######## ##.#     ", // 16
        "     #.##          ##.#     ", // 17
        "######.##.########.##.######", // 18
        "#............##............#", // 19
        "#.####.#####.##.#####.####.#", // 20
        "#O..##................##..O#", // 21
        "###.##.##.########.##.##.###", // 22
        "#......##....  ....##......#", // 23
        "#.######.###.##.###.######.#", // 24
        "#..........#.##.#..........#", // 25
        "#.########.#.##.#.########.#", // 26
        "#.#......#........#......#.#", // 27
        "#.#.####.##########.####.#.#", // 28
        "#..........................#", // 29
        "############################", // 30
    };

    public static char GetTile(int col, int row)
    {
        if (row < 0 || row >= Height) return '#';
        if (row == TunnelRow && (col < 0 || col >= Width)) return ' ';
        if (col < 0 || col >= Width) return '#';
        return Layout[row][col];
    }

    public static bool IsWall(int col, int row, bool allowGhostGate = false)
    {
        char tile = GetTile(col, row);
        if (tile == '#') return true;
        if (tile == '-' && !allowGhostGate) return true;
        return false;
    }

    public static int WrapCol(int col)
    {
        if (col < 0) return Width - 1;
        if (col >= Width) return 0;
        return col;
    }

    public static Vector3 GridToWorld(int col, int row)
    {
        return new Vector3(col - 13.5f, 15.0f - row, 0f);
    }

    public static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int col = Mathf.RoundToInt(worldPos.x + 13.5f);
        int row = Mathf.RoundToInt(15.0f - worldPos.y);
        return new Vector2Int(col, row);
    }
}
