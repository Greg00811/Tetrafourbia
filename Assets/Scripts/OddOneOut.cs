using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Microgame: "Odd One Out"
///
/// 4 squares are shown to the player.
/// 3 squares show images from the Common Images pool.
/// 1 square shows an image from the Odd Images pool.
///
/// Clicking the odd square = WIN.
/// Clicking any other square = LOSE.
///
/// Example: Common pool = drawings of 3 (3 apples, 3 stars, 3 cats...)
///          Odd pool    = drawings of 4 (4 apples, 4 stars, 4 cats...)
///
/// No image is ever used on two squares in the same round
/// (unless you turn on "Allow Duplicate Commons").
///
/// Setup:
///   1. Create a child GameObject under your microgame container.
///   2. Add 4 Buttons, each with an Image component (these are your squares).
///   3. Attach this script to the root of that GameObject.
///   4. Drag the 4 Buttons into the "Squares" array.
///   5. Fill "Common Images" (at least 3) and "Odd Images" (at least 1).
///   6. Drag this GameObject into MicrogameManager's
///      "Available Microgames" list, then disable it in the Hierarchy.
/// </summary>
public class OddOneOut : MicrogameBase
{
    private const int RequiredSquares = 4;
    private const int CommonCount = RequiredSquares - 1;

    [SerializeField] private Button[] squares;

    [Header("Image Pools")]
    [Tooltip("Images that can appear on the 3 normal squares. Needs at least 3.")]
    [SerializeField] private Sprite[] commonImages;

    [Tooltip("Images that can appear on the 1 odd square. Needs at least 1.")]
    [SerializeField] private Sprite[] oddImages;

    [Header("Rules")]
    [Tooltip("If false, the 3 common squares always show 3 different images.")]
    [SerializeField] private bool allowDuplicateCommons = false;

    // The one button that holds the odd image.
    private Button correctSquare;

    private bool gameActive;

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        if (squares == null || squares.Length < RequiredSquares)
        {
            Debug.LogError("OddOneOut: You need " + RequiredSquares + " squares assigned!");
            return;
        }

        if (commonImages == null || commonImages.Length == 0)
        {
            Debug.LogError("OddOneOut: Common Images is empty!");
            return;
        }

        if (!allowDuplicateCommons && commonImages.Length < CommonCount)
        {
            Debug.LogError("OddOneOut: Common Images needs at least " + CommonCount +
                           " images (or enable Allow Duplicate Commons)!");
            return;
        }

        if (oddImages == null || oddImages.Length == 0)
        {
            Debug.LogError("OddOneOut: Odd Images is empty!");
            return;
        }

        // Choose the 3 common sprites for this round.
        List<Sprite> chosenCommons = PickCommonSprites();

        // Choose the odd sprite, making sure it isn't the same as any common one.
        List<Sprite> validOdds = new List<Sprite>();

        foreach (Sprite candidate in oddImages)
        {
            if (candidate != null && !chosenCommons.Contains(candidate))
            {
                validOdds.Add(candidate);
            }
        }

        if (validOdds.Count == 0)
        {
            Debug.LogError("OddOneOut: No Odd Image available that differs from the chosen common images!");
            return;
        }

        Sprite oddSprite = validOdds[Random.Range(0, validOdds.Count)];

        // Pick exactly ONE square to be the odd one.
        int correctIndex = Random.Range(0, RequiredSquares);

        correctSquare = squares[correctIndex];

        gameActive = true;

        int commonIndex = 0;

        for (int i = 0; i < RequiredSquares; i++)
        {
            if (squares[i] == null)
                continue;

            Button currentButton = squares[i];

            // Get the Image component on the Button.
            Image image = currentButton.GetComponent<Image>();

            if (image != null)
            {
                // ONE square gets the odd sprite.
                // EVERY other square gets the next common sprite.
                if (i == correctIndex)
                {
                    image.sprite = oddSprite;
                }
                else
                {
                    image.sprite = chosenCommons[commonIndex];
                    commonIndex++;
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
        }
    }

    private List<Sprite> PickCommonSprites()
    {
        List<Sprite> result = new List<Sprite>();

        if (allowDuplicateCommons)
        {
            // Any common image can be used on any of the 3 squares, repeats allowed.
            for (int i = 0; i < CommonCount; i++)
            {
                result.Add(commonImages[Random.Range(0, commonImages.Length)]);
            }

            return result;
        }

        // Shuffle a copy of the pool, then take the first 3 (so none repeat).
        List<Sprite> pool = new List<Sprite>();

        foreach (Sprite sprite in commonImages)
        {
            // Skip empty slots and the same sprite listed twice.
            if (sprite != null && !pool.Contains(sprite))
            {
                pool.Add(sprite);
            }
        }

        if (pool.Count < CommonCount)
        {
            Debug.LogError("OddOneOut: Not enough different Common Images (need " + CommonCount + ")!");
            return pool;
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Sprite temp = pool[i];
            pool[i] = pool[j];
            pool[j] = temp;
        }

        for (int i = 0; i < CommonCount; i++)
        {
            result.Add(pool[i]);
        }

        return result;
    }

    private void HandleSquareClicked(Button clickedButton)
    {
        if (!gameActive)
            return;

        gameActive = false;

        DisableAllSquares();

        // ONLY the odd square wins.
        ReportResult(clickedButton == correctSquare);
    }

    private void DisableAllSquares()
    {
        if (squares == null)
            return;

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
        // Ran out of time without choosing - disable input.
        gameActive = false;

        DisableAllSquares();
    }
}