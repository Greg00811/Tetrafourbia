using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Example microgame: "TAP 4x!" - the player must tap a button exactly
/// 4 times before the timer runs out. Tapping a 5th time is a loss
/// (punishes button-mashing, forces players to actually count).
///
/// Setup:
///   1. Create a child GameObject under your microgame container.
///   2. Add a Button + TMP label inside it (e.g. "TAP!").
///   3. Add a TMP_Text to show the running tap count.
///   4. Attach this script to the root of that GameObject.
///   5. Drag the Button and the tap-count Text into this script's fields.
///   6. Drag this whole GameObject into MicrogameManager's
///      "Available Microgames" list, then disable it in the Hierarchy
///      (the manager activates it only when it's this game's turn).
/// </summary>
public class TapFourTimes : MicrogameBase
{
    [SerializeField] private Button tapButton;
    [SerializeField] private TMP_Text tapCountText;

    private const int RequiredTaps = 4;
    private int currentTaps;

    public override void StartMicrogame()
    {
        base.StartMicrogame();
        currentTaps = 0;
        UpdateTapCountUI();

        if (tapButton != null)
        {
            tapButton.onClick.RemoveAllListeners();
            tapButton.onClick.AddListener(HandleTap);
            tapButton.interactable = true;
        }
    }

    private void HandleTap()
    {
        currentTaps++;
        UpdateTapCountUI();

        if (currentTaps == RequiredTaps)
        {
            if (tapButton != null) tapButton.interactable = false;
            ReportResult(true); // exact match = win
        }
        else if (currentTaps > RequiredTaps)
        {
            if (tapButton != null) tapButton.interactable = false;
            ReportResult(false); // overshot = loss
        }
    }

    private void UpdateTapCountUI()
    {
        if (tapCountText != null)
            tapCountText.text = $"{currentTaps} / {RequiredTaps}";
    }

    public override void ForceStop()
    {
        // Ran out of time without hitting exactly 4 - disable input.
        if (tapButton != null) tapButton.interactable = false;
    }
}
