using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Microgame: "Find the Right Square"
///
/// 5 squares bounce around the screen.
/// 4 squares use the Normal Sprite.
/// 1 square uses the Target Sprite.
///
/// Clicking the Target Square = WIN.
/// Clicking any Normal Square = LOSE.
/// </summary>
public class BouncingSquares : MicrogameBase
{
    [SerializeField] private Button[] squares;

    [Header("Square Appearance")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite targetSprite;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 250f;

    private Vector2[] velocities;

    // The one button that contains the target.
    private Button correctSquare;

    private bool gameActive;

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        if (squares == null || squares.Length < 5)
        {
            Debug.LogError("BouncingSquares: You need at least 5 squares assigned!");
            return;
        }

        if (normalSprite == null)
        {
            Debug.LogError("BouncingSquares: Normal Sprite is not assigned!");
            return;
        }

        if (targetSprite == null)
        {
            Debug.LogError("BouncingSquares: Target Sprite is not assigned!");
            return;
        }

        velocities = new Vector2[squares.Length];

        // Pick exactly ONE square to be the target.
        int correctIndex = Random.Range(0, squares.Length);

        correctSquare = squares[correctIndex];

        gameActive = true;

        for (int i = 0; i < squares.Length; i++)
        {
            if (squares[i] == null)
                continue;

            int index = i;
            Button currentButton = squares[i];

            // Get the Image component on the Button.
            Image image = currentButton.GetComponent<Image>();

            if (image != null)
            {
                // ONE square gets the target.
                // EVERY other square gets the normal sprite.
                if (i == correctIndex)
                {
                    image.sprite = targetSprite;
                }
                else
                {
                    image.sprite = normalSprite;
                }

                image.color = Color.white;
            }

            // Remove listeners from a previous round.
            currentButton.onClick.RemoveAllListeners();

            // Give each button its own click listener.
            currentButton.onClick.AddListener(() =>
            {
                HandleSquareClicked(currentButton);
            });

            currentButton.interactable = true;

            // Give each square a random movement direction.
            Vector2 direction = Random.insideUnitCircle.normalized;

            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            velocities[i] = direction * moveSpeed;

            // Give each square a random starting position.
            RectTransform rect = currentButton.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.anchoredPosition = GetRandomPosition(rect);
            }
        }
    }

    private void Update()
    {
        if (!gameActive)
            return;

        for (int i = 0; i < squares.Length; i++)
        {
            if (squares[i] == null)
                continue;

            MoveSquare(i);
        }
    }

    private Vector2 GetRandomPosition(RectTransform square)
    {
        RectTransform parentRect = square.parent as RectTransform;

        if (parentRect == null)
            return Vector2.zero;

        float halfWidth = parentRect.rect.width * 0.5f;
        float halfHeight = parentRect.rect.height * 0.5f;

        float squareHalfWidth = square.rect.width * 0.5f;
        float squareHalfHeight = square.rect.height * 0.5f;

        float x = Random.Range(
            -halfWidth + squareHalfWidth,
            halfWidth - squareHalfWidth
        );

        float y = Random.Range(
            -halfHeight + squareHalfHeight,
            halfHeight - squareHalfHeight
        );

        return new Vector2(x, y);
    }

    private void MoveSquare(int index)
    {
        RectTransform square = squares[index].GetComponent<RectTransform>();

        if (square == null)
            return;

        RectTransform parentRect = square.parent as RectTransform;

        if (parentRect == null)
            return;

        square.anchoredPosition += velocities[index] * Time.deltaTime;

        float halfWidth = parentRect.rect.width * 0.5f;
        float halfHeight = parentRect.rect.height * 0.5f;

        float squareHalfWidth = square.rect.width * 0.5f;
        float squareHalfHeight = square.rect.height * 0.5f;

        float minX = -halfWidth + squareHalfWidth;
        float maxX = halfWidth - squareHalfWidth;

        float minY = -halfHeight + squareHalfHeight;
        float maxY = halfHeight - squareHalfHeight;

        Vector2 position = square.anchoredPosition;

        // Bounce off the left/right edges.
        if (position.x <= minX)
        {
            position.x = minX;
            velocities[index].x = Mathf.Abs(velocities[index].x);
        }
        else if (position.x >= maxX)
        {
            position.x = maxX;
            velocities[index].x = -Mathf.Abs(velocities[index].x);
        }

        // Bounce off the top/bottom edges.
        if (position.y <= minY)
        {
            position.y = minY;
            velocities[index].y = Mathf.Abs(velocities[index].y);
        }
        else if (position.y >= maxY)
        {
            position.y = maxY;
            velocities[index].y = -Mathf.Abs(velocities[index].y);
        }

        square.anchoredPosition = position;
    }

    private void HandleSquareClicked(Button clickedButton)
    {
        if (!gameActive)
            return;

        gameActive = false;

        DisableAllSquares();

        // ONLY the target square wins.
        if (clickedButton == correctSquare)
        {
            ReportResult(true);
        }
        else
        {
            ReportResult(false);
        }
    }

    private void DisableAllSquares()
    {
        foreach (Button square in squares)
        {
            if (square != null)
            {
                square.interactable = false;
            }
        }
    }

    public override void ForceStop()
    {
        gameActive = false;

        DisableAllSquares();
    }
}