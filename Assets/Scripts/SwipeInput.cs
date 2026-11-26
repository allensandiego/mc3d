using UnityEngine;

/// <summary>
/// Camera-relative swipe & keyboard controls for over-the-shoulder Pac-Man:
/// Swipe Left = 90 deg left, Swipe Right = 90 deg right, Swipe Down = 180 deg U-turn, Swipe Up = straight.
/// </summary>
public class SwipeInput : MonoBehaviour
{
    [SerializeField] private float minSwipeDistancePixels = 30f;

    public Vector2Int BufferedDirection { get; private set; } = Vector2Int.zero;
    public bool TapOrActionPressed { get; private set; }

    private PacManController pacMan;
    private Vector2 dragStart;
    private bool isDragging;
    private bool swipeTriggeredThisTouch;

    public void BindPacMan(PacManController controller)
    {
        pacMan = controller;
    }

    private void Update()
    {
        TapOrActionPressed = false;

        HandleKeyboard();
        HandleTouch();
        HandleMouseDrag();
    }

    public void ClearBufferedDirection()
    {
        BufferedDirection = Vector2Int.zero;
    }

    private Vector2Int GetCurrentDir()
    {
        return pacMan != null && pacMan.Direction != Vector2Int.zero
            ? pacMan.Direction
            : MazeData.GridLeft;
    }

    private static Vector2Int RotateLeft(Vector2Int d) => new Vector2Int(d.y, -d.x);
    private static Vector2Int RotateRight(Vector2Int d) => new Vector2Int(-d.y, d.x);

    private void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            BufferedDirection = RotateLeft(GetCurrentDir());
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            BufferedDirection = RotateRight(GetCurrentDir());
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            BufferedDirection = -GetCurrentDir();
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            BufferedDirection = GetCurrentDir();

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            TapOrActionPressed = true;
    }

    private void HandleTouch()
    {
        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                dragStart = touch.position;
                isDragging = true;
                swipeTriggeredThisTouch = false;
                break;

            case TouchPhase.Moved:
                if (isDragging)
                {
                    Vector2 delta = touch.position - dragStart;
                    if (delta.magnitude >= minSwipeDistancePixels)
                    {
                        ApplySwipeDelta(delta);
                        dragStart = touch.position;
                        swipeTriggeredThisTouch = true;
                    }
                }
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (isDragging && !swipeTriggeredThisTouch)
                {
                    Vector2 delta = touch.position - dragStart;
                    if (delta.magnitude >= minSwipeDistancePixels)
                        ApplySwipeDelta(delta);
                    else
                        TapOrActionPressed = true;
                }
                isDragging = false;
                break;
        }
    }

    private void HandleMouseDrag()
    {
        if (Input.touchCount > 0) return;

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = Input.mousePosition;
            isDragging = true;
            swipeTriggeredThisTouch = false;
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 delta = (Vector2)Input.mousePosition - dragStart;
            if (delta.magnitude >= minSwipeDistancePixels)
            {
                ApplySwipeDelta(delta);
                dragStart = Input.mousePosition;
                swipeTriggeredThisTouch = true;
            }
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            if (!swipeTriggeredThisTouch)
                TapOrActionPressed = true;
            isDragging = false;
        }
    }

    private void ApplySwipeDelta(Vector2 delta)
    {
        Vector2Int cur = GetCurrentDir();
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            BufferedDirection = delta.x > 0 ? RotateRight(cur) : RotateLeft(cur);
        }
        else
        {
            BufferedDirection = delta.y > 0 ? cur : -cur;
        }
    }
}
