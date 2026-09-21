using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Runs the WarioWare-style microgame loop, themed around FOUR:
///   - Player starts with 4 lives
///   - Each microgame gets a shrinking time limit (starts near 4 seconds)
///   - Every 4 microgames cleared, speed ramps up a notch
///   - Session length comes from the main menu's tier choice
///     (4 / 8 / 16 / 32 games), read from PlayerPrefs("SelectedMicrogameCount")
///
/// Setup:
///   1. Put each microgame on its own child GameObject under a common parent,
///      each with a script inheriting MicrogameBase. Disable them all by default.
///   2. Drag those GameObjects into "Available Microgames" below.
///   3. Wire up the UI references (instruction text, timer bar, lives text).
///   4. Put this script on an empty "MicrogameManager" GameObject in your
///      gameplay scene (the one FourMainMenu's Start button loads).
/// </summary>
public class MicrogameManager : MonoBehaviour
{
    [Header("Available Microgames (each a disabled child GameObject)")]
    [SerializeField] private GameObject[] microgamePrefabsOrObjects;

    [Header("Session Settings")]
    [SerializeField] private int startingLives = 4;
    [SerializeField] private int speedRampEveryNGames = 4; // ramp up every 4 clears
    [SerializeField] private float speedRampMultiplier = 0.9f; // time limit shrinks 10% per ramp
    [SerializeField] private float minimumTimeLimit = 1.2f;

    [Header("UI References")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text gameCountText;
    [SerializeField] private Image timerBarFill; // Image with Fill Type = Filled
    [SerializeField] private GameObject readyBanner;  // shows "READY?" between rounds
    [SerializeField] private GameObject goBanner;      // shows "GO!" when round starts
    [SerializeField] private CanvasGroup resultsPanel; // shown at end of session
    [SerializeField] private TMP_Text resultsSummaryText;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int totalGamesThisSession;
    private int gamesClearedCount;
    private int currentLives;
    private float currentTimeLimitMultiplier = 1f;
    private List<GameObject> shuffledBag = new List<GameObject>();
    private MicrogameBase activeMicrogame;
    private Coroutine roundRoutine;

    private void Start()
    {
        totalGamesThisSession = PlayerPrefs.GetInt("SelectedMicrogameCount", 4);
        currentLives = startingLives;

        foreach (var obj in microgamePrefabsOrObjects)
        {
            if (obj != null) obj.SetActive(false);
        }

        if (resultsPanel != null)
        {
            resultsPanel.alpha = 0f;
            resultsPanel.gameObject.SetActive(false);
        }

        UpdateLivesUI();
        UpdateGameCountUI();

        roundRoutine = StartCoroutine(RunSession());
    }

    private IEnumerator RunSession()
    {
        while (gamesClearedCount < totalGamesThisSession && currentLives > 0)
        {
            yield return StartCoroutine(PlayOneRound());
        }

        EndSession();
    }

    private IEnumerator PlayOneRound()
    {
        // Ramp difficulty every N clears.
        if (gamesClearedCount > 0 && gamesClearedCount % speedRampEveryNGames == 0)
        {
            currentTimeLimitMultiplier *= speedRampMultiplier;
        }

        GameObject chosenObject = PickNextMicrogame();
        activeMicrogame = chosenObject.GetComponent<MicrogameBase>();

        if (activeMicrogame == null)
        {
            Debug.LogError($"{chosenObject.name} has no MicrogameBase-derived component!");
            yield break;
        }

        // "READY?" beat
        if (readyBanner != null) readyBanner.SetActive(true);
        if (instructionText != null) instructionText.text = activeMicrogame.Instruction;
        yield return new WaitForSeconds(0.8f);
        if (readyBanner != null) readyBanner.SetActive(false);

        // "GO!" beat
        if (goBanner != null) goBanner.SetActive(true);
        yield return new WaitForSeconds(0.4f);
        if (goBanner != null) goBanner.SetActive(false);

        // Run the actual microgame with a shrinking timer.
        chosenObject.SetActive(true);
        activeMicrogame.StartMicrogame();

        float timeLimit = Mathf.Max(minimumTimeLimit, activeMicrogame.BaseTimeLimit * currentTimeLimitMultiplier);
        float timeRemaining = timeLimit;
        bool resultReceived = false;
        bool won = false;

        void HandleResult(bool result)
        {
            resultReceived = true;
            won = result;
        }

        activeMicrogame.OnMicrogameResult += HandleResult;

        while (timeRemaining > 0f && !resultReceived)
        {
            timeRemaining -= Time.deltaTime;
            activeMicrogame.TickMicrogame(Time.deltaTime);

            if (timerBarFill != null)
                timerBarFill.fillAmount = Mathf.Clamp01(timeRemaining / timeLimit);

            yield return null;
        }

        activeMicrogame.OnMicrogameResult -= HandleResult;

        if (!resultReceived)
        {
            // Ran out of time without a reported result = automatic loss.
            activeMicrogame.ForceStop();
            won = false;
        }

        chosenObject.SetActive(false);

        if (won)
        {
            gamesClearedCount++;
        }
        else
        {
            currentLives--;
        }

        UpdateLivesUI();
        UpdateGameCountUI();

        yield return new WaitForSeconds(0.3f); // brief beat before next round
    }

    private GameObject PickNextMicrogame()
    {
        // "Shuffled bag" approach: avoids repeats until every microgame has
        // played once, then reshuffles. Feels more varied than pure random.
        if (shuffledBag.Count == 0)
        {
            shuffledBag.AddRange(microgamePrefabsOrObjects);
            for (int i = shuffledBag.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (shuffledBag[i], shuffledBag[swapIndex]) = (shuffledBag[swapIndex], shuffledBag[i]);
            }
        }

        GameObject next = shuffledBag[0];
        shuffledBag.RemoveAt(0);
        return next;
    }

    private void UpdateLivesUI()
    {
        if (livesText != null)
            livesText.text = $"LIVES: {currentLives}";
    }

    private void UpdateGameCountUI()
    {
        if (gameCountText != null)
            gameCountText.text = $"{gamesClearedCount} / {totalGamesThisSession}";
    }

    private void EndSession()
    {
        bool clearedAll = gamesClearedCount >= totalGamesThisSession;

        if (resultsPanel != null)
        {
            resultsPanel.gameObject.SetActive(true);
            resultsPanel.alpha = 1f;
        }

        if (resultsSummaryText != null)
        {
            resultsSummaryText.text = clearedAll
                ? $"CLEARED ALL {totalGamesThisSession}!"
                : $"GAME OVER\n{gamesClearedCount} / {totalGamesThisSession} cleared";
        }
    }

    /// <summary>Hook this up to a "Back to Menu" button on the results panel.</summary>
    public void ReturnToMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
