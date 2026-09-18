using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu controller for a WarioWare-style microgame collection
/// themed around the number FOUR.
///
/// Concept: everything comes in fours.
///   - 4 menu options, arranged in a spinning "+"-shaped cross
///   - A "4-second" countdown pulse plays behind the menu (idle animation)
///   - Pressing Start triggers a quad-flash transition (4 flashes) before loading
///   - Difficulty is chosen in 4 tiers: 4, 8, 16, 32 microgames per run
///
/// Setup:
///   1. Create a Canvas with 4 Buttons (Start, Tiers, Extras, Quit) and
///      assign them in the inspector.
///   2. Add a CanvasGroup to a full-screen white Image for the flash transition.
///   3. Attach this script to an empty "MenuController" GameObject.
/// </summary>
public class FourMainMenu : MonoBehaviour
{
    [Header("Core Menu Buttons (exactly 4)")]
    [SerializeField] private Button[] menuButtons = new Button[4];
    [SerializeField] private string[] buttonLabels = { "START", "TIERS", "EXTRAS", "QUIT" };

    [Header("Countdown / Idle Pulse")]
    [SerializeField] private Text countdownText;
    [SerializeField] private float pulseInterval = 1f; // ticks 4,3,2,1 then loops
    [SerializeField] private RectTransform logoCross;   // the "+"-shaped 4 logo
    [SerializeField] private float spinSpeed = 45f;

    [Header("Difficulty Tiers")]
    [SerializeField] private int[] tierGameCounts = { 4, 8, 16, 32 };
    private int currentTierIndex = 0;
    [SerializeField] private Text tierDisplayText;

    [Header("Transition")]
    [SerializeField] private CanvasGroup flashOverlay;
    [SerializeField] private int flashCount = 4;
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private string gameplaySceneName = "MicrogameLoop";

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip blipSFX;
    [SerializeField] private AudioClip confirmSFX;

    private Coroutine countdownRoutine;

    private void Awake()
    {
        // Wire up buttons defensively in case the inspector array is out of order.
        if (menuButtons.Length != 4)
        {
            Debug.LogWarning("FourMainMenu expects exactly 4 buttons for thematic consistency.");
        }

        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i; // capture for closure
            if (menuButtons[i] == null) continue;

            var label = menuButtons[i].GetComponentInChildren<Text>();
            if (label != null && index < buttonLabels.Length)
                label.text = buttonLabels[index];

            menuButtons[i].onClick.AddListener(() => OnMenuButtonPressed(index));
        }

        UpdateTierDisplay();
    }

    private void OnEnable()
    {
        countdownRoutine = StartCoroutine(CountdownPulse());
    }

    private void OnDisable()
    {
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
    }

    private void Update()
    {
        // Idle spin on the "4" logo cross — purely decorative flair.
        if (logoCross != null)
        {
            logoCross.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Ticks 4 -> 3 -> 2 -> 1 -> (flash) -> repeat, WarioWare-style,
    /// just as ambient menu energy. Doesn't auto-start the game.
    /// </summary>
    private IEnumerator CountdownPulse()
    {
        while (true)
        {
            for (int count = 4; count >= 1; count--)
            {
                if (countdownText != null)
                    countdownText.text = count.ToString();

                PlayBlip();
                yield return StartCoroutine(PunchScale(countdownText != null ? countdownText.rectTransform : null));
                yield return new WaitForSeconds(pulseInterval);
            }
        }
    }

    private IEnumerator PunchScale(RectTransform target)
    {
        if (target == null) yield break;
        Vector3 original = Vector3.one;
        Vector3 punch = Vector3.one * 1.3f;
        float t = 0f;
        float duration = 0.15f;

        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(punch, original, t / duration);
            yield return null;
        }
        target.localScale = original;
    }

    private void OnMenuButtonPressed(int index)
    {
        PlayConfirm();

        switch (index)
        {
            case 0: // START
                StartCoroutine(StartGameSequence());
                break;
            case 1: // TIERS - cycle through the 4 difficulty tiers
                CycleTier();
                break;
            case 2: // EXTRAS
                Debug.Log("Open extras/credits screen (implement scene or panel toggle here).");
                break;
            case 3: // QUIT
                QuitGame();
                break;
        }
    }

    private void CycleTier()
    {
        currentTierIndex = (currentTierIndex + 1) % tierGameCounts.Length;
        UpdateTierDisplay();
    }

    private void UpdateTierDisplay()
    {
        if (tierDisplayText != null)
        {
            tierDisplayText.text = $"{tierGameCounts[currentTierIndex]} GAMES";
        }
        // Persist selection for the gameplay scene to read.
        PlayerPrefs.SetInt("SelectedMicrogameCount", tierGameCounts[currentTierIndex]);
    }

    /// <summary>
    /// Quad-flash transition: flashes the overlay 4 times before loading
    /// the gameplay scene, echoing the classic WarioWare "ready?" beat.
    /// </summary>
    private IEnumerator StartGameSequence()
    {
        foreach (var btn in menuButtons)
        {
            if (btn != null) btn.interactable = false;
        }

        if (flashOverlay != null)
        {
            for (int i = 0; i < flashCount; i++)
            {
                yield return FadeCanvasGroup(flashOverlay, 0f, 1f, flashDuration);
                yield return FadeCanvasGroup(flashOverlay, 1f, 0f, flashDuration);
            }
        }
        else
        {
            // Fallback delay if no overlay assigned.
            yield return new WaitForSeconds(flashDuration * flashCount * 2f);
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private void PlayBlip()
    {
        if (audioSource != null && blipSFX != null)
            audioSource.PlayOneShot(blipSFX);
    }

    private void PlayConfirm()
    {
        if (audioSource != null && confirmSFX != null)
            audioSource.PlayOneShot(confirmSFX);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
