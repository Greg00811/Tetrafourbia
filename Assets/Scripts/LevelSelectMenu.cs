using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The level select screen on the main menu. It shows one button per
/// microgame; tapping one tells the menu which level was chosen, and the
/// gameplay scene then replays just that microgame.
///
/// The buttons are created for you from ONE button prefab, so you design a
/// single button and the list builds itself from the names below.
///
/// IMPORTANT: put this script on an object that stays active (for example your
/// MenuController or the Canvas). The screen it shows and hides is a separate
/// child object assigned to "Panel Root".
///
/// The order of "Level Names" must match the order of the microgames in
/// MicroGameManager's "Microgame Prefabs Or Objects" list. If you have a
/// finale, put its name last: it becomes the final level.
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    /// <summary>
    /// The PlayerPrefs key the gameplay scene reads.
    /// -1 means a normal run; 0, 1, 2... means "replay just that microgame".
    /// </summary>
    public const string PrefKey = "SelectedLevel";

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;            // the screen that gets shown/hidden
    [SerializeField] private Transform buttonContainer;       // where the level buttons are created (give it a Vertical or Grid Layout Group)
    [SerializeField] private Button levelButtonPrefab;        // a Button prefab with a TMP label inside
    [SerializeField] private Button backButton;

    [Header("Levels (same order as the manager's microgame list)")]
    [SerializeField] private string[] levelNames =
    {
        "TAP FOUR TIMES", "ODD ONE OUT", "BOUNCING SQUARES", "GALAGA BLAST", "LIGHTS & DOORS"
    };

    /// <summary>Fired with the chosen level's index when the player taps a level.</summary>
    public event Action<int> OnLevelChosen;

    private readonly List<Button> levelButtons = new List<Button>();
    private bool built;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open()
    {
        BuildButtonsOnce();

        foreach (Button button in levelButtons)
        {
            if (button != null) button.interactable = true;
        }

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void BuildButtonsOnce()
    {
        if (built) return;

        if (levelButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("LevelSelectMenu: assign 'Level Button Prefab' and 'Button Container'.");
            return;
        }

        for (int i = 0; i < levelNames.Length; i++)
        {
            int levelIndex = i; // capture for the click handler

            Button button = Instantiate(levelButtonPrefab, buttonContainer);
            button.gameObject.SetActive(true);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = levelNames[i];

            button.onClick.AddListener(() => ChooseLevel(levelIndex));
            levelButtons.Add(button);
        }

        built = true;
    }

    private void ChooseLevel(int levelIndex)
    {
        // Lock every button so a double tap can't start the level twice.
        foreach (Button button in levelButtons)
        {
            if (button != null) button.interactable = false;
        }

        OnLevelChosen?.Invoke(levelIndex);
    }
}
