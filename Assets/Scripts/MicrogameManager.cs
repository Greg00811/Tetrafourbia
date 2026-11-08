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
///   - START plays a normal run of "Normal Run Length" games, then the finale
///   - LEVELS on the menu replays one chosen microgame for "Practice Rounds" rounds
///     (the choice is read from PlayerPrefs, saved by the menu)
///
/// Setup:
///   1. Put each microgame on its own child GameObject under a common parent,
///      each with a script inheriting MicrogameBase. Disable them all by default.
///   2. Drag those GameObjects into "Available Microgames" below.
///   3. Wire up the UI references (instruction text, timer slider, lives text).
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

    [Header("Run Lengths")]
    [SerializeField] private int normalRunLength = 8;  // games in a normal run from START
    [SerializeField] private int practiceRounds = 4;   // rounds when replaying one level from LEVELS

    [Header("UI References")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text gameCountText;
    [SerializeField] private Slider timerSlider; // drag the "Timer" object (the one with the Slider component) here
    [SerializeField] private GameObject readyBanner;  // shows "READY?" between rounds
    [SerializeField] private GameObject goBanner;      // shows "GO!" when round starts
    [SerializeField] private CanvasGroup resultsPanel; // shown at end of session
    [SerializeField] private TMP_Text resultsSummaryText;
    [SerializeField] private GameObject[] winObjects;   // shown only when the player wins
    [SerializeField] private GameObject[] loseObjects;  // shown only when the player loses

    [Header("Narrative (tap-to-advance dialogue)")]
    [SerializeField] private DialoguePlayer dialoguePlayer;
    [SerializeField] private NarrativeSequence introSequence;          // plays once, before the first microgame
    [SerializeField] private NarrativeSequence[] interludeSequences;   // played in order, one per break
    [SerializeField] private int gamesPerInterlude = 4;                // break after every 4 rounds played

    [Header("Between-Round Lives Break")]
    [SerializeField] private LivesBreakScreen livesBreakScreen;

    [Header("Finale (played once after every other game is cleared)")]
    [Tooltip("Untick to skip the final boss minigame completely.")]
    [SerializeField] private bool finaleEnabled = true;
    [SerializeField] private GameObject finaleMicrogame;
    [SerializeField] private NarrativeSequence finaleIntroSequence;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int totalGamesThisSession;
    private int gamesClearedCount;
    private int roundsPlayed;
    private int interludeIndex;
    private bool lastRoundWon;
    private bool finaleWon;
    private bool sessionBroken; // set if setup is wrong, so we stop instead of erroring every frame
    private GameObject practiceMicrogame; // set when replaying a single level
    private int currentLives;
    private float currentTimeLimitMultiplier = 1f;
    private List<GameObject> shuffledBag = new List<GameObject>();
    private MicrogameBase activeMicrogame;
    private Coroutine roundRoutine;

    /// <summary>True only if the finale checkbox is ticked AND a finale object is assigned.</summary>
    private bool FinaleActive => finaleEnabled && finaleMicrogame != null;

    /// <summary>Your custom font has no zero, so every 0 is shown as a capital O.</summary>
    private static string NoZeros(string s) => s.Replace('0', 'O');

    private void Start()
    {
        // Set up the timer slider so it behaves as a 0-1 display bar
        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = 1f;
            timerSlider.wholeNumbers = false;
            timerSlider.interactable = false; // so the player can't drag it
            timerSlider.value = 1f;
        }

        // -1 = normal run. 0, 1, 2... = replay that one microgame (the finale is the entry after the list).
        int level = PlayerPrefs.GetInt(LevelSelectMenu.PrefKey, -1);
        practiceMicrogame = null;

        if (level >= 0 && microgamePrefabsOrObjects != null)
        {
            if (level < microgamePrefabsOrObjects.Length)
                practiceMicrogame = microgamePrefabsOrObjects[level];
            else if (level == microgamePrefabsOrObjects.Length && FinaleActive)
                practiceMicrogame = finaleMicrogame;
        }

        if (practiceMicrogame == null)
            totalGamesThisSession = normalRunLength;
        else
            totalGamesThisSession = practiceMicrogame == finaleMicrogame ? 1 : practiceRounds; // the finale is long, so once is enough
        currentLives = startingLives;

        foreach (var obj in microgamePrefabsOrObjects)
        {
            if (obj != null) obj.SetActive(false);
        }

        if (finaleMicrogame != null) finaleMicrogame.SetActive(false);

        if (resultsPanel != null)
        {
            resultsPanel.alpha = 0f;
            resultsPanel.gameObject.SetActive(false);
        }

        // Hide the win/lose visuals until the session ends
        SetObjectsActive(winObjects, false);
        SetObjectsActive(loseObjects, false);

        UpdateLivesUI();
        UpdateGameCountUI();

        roundRoutine = StartCoroutine(RunSession());
    }

    private IEnumerator RunSession()
    {
        // Story beat before anything else starts.
        if (practiceMicrogame == null) yield return StartCoroutine(PlayNarrative(introSequence));

        while (gamesClearedCount < totalGamesThisSession && currentLives > 0 && !sessionBroken)
        {
            int livesBefore = currentLives;
            int clearedBefore = gamesClearedCount;

            yield return StartCoroutine(PlayOneRound());
            roundsPlayed++;

            bool wonRound = gamesClearedCount > clearedBefore;
            bool lostLife = currentLives < livesBefore;

            // Lives check between rounds. Skipped once every game is cleared,
            // since the results screen takes over from there.
            bool allCleared = gamesClearedCount >= totalGamesThisSession;
            if (!allCleared && livesBreakScreen != null && (wonRound || lostLife))
            {
                yield return StartCoroutine(livesBreakScreen.Show(currentLives, lostLife, wonRound));
            }

            // Story beat after every Nth round - but not if the session just ended,
            // since the results screen takes over from there.
            bool sessionOver = gamesClearedCount >= totalGamesThisSession || currentLives <= 0;
            bool interludeDue = gamesPerInterlude > 0 && roundsPlayed % gamesPerInterlude == 0;

            if (practiceMicrogame == null && !sessionOver && interludeDue
                && interludeSequences != null && interludeIndex < interludeSequences.Length)
            {
                yield return StartCoroutine(PlayNarrative(interludeSequences[interludeIndex]));
                interludeIndex++;
            }
        }

        // Finale: only if it is enabled and every normal game was cleared with lives to spare.
        // Losing it costs a life and replays it until you win or run out of lives.
        if (practiceMicrogame == null && FinaleActive && gamesClearedCount >= totalGamesThisSession && currentLives > 0)
        {
            yield return StartCoroutine(PlayNarrative(finaleIntroSequence));

            while (!finaleWon && currentLives > 0)
            {
                yield return StartCoroutine(PlayOneRound(finaleMicrogame, false));
                finaleWon = lastRoundWon;

                if (!finaleWon && currentLives > 0 && livesBreakScreen != null)
                {
                    yield return StartCoroutine(livesBreakScreen.Show(currentLives, true, false));
                }
            }
        }

        EndSession();
    }

    /// <summary>Plays a dialogue sequence and waits for it to finish. Safely does nothing if unassigned.</summary>
    private IEnumerator PlayNarrative(NarrativeSequence sequence)
    {
        if (dialoguePlayer == null || sequence == null) yield break;
        yield return StartCoroutine(dialoguePlayer.Play(sequence));
    }

    private IEnumerator PlayOneRound(GameObject forcedMicrogame = null, bool countsTowardTotal = true)
    {
        // Ramp difficulty every N clears (not for the finale, which is picked by hand).
        if (forcedMicrogame == null && gamesClearedCount > 0 && gamesClearedCount % speedRampEveryNGames == 0)
        {
            currentTimeLimitMultiplier *= speedRampMultiplier;
        }

        GameObject chosenObject = forcedMicrogame != null ? forcedMicrogame : PickNextMicrogame();

        if (chosenObject == null)
        {
            Debug.LogError("MicrogameManager: there are no microgames to play. The 'Microgame Prefabs Or Objects' list on THIS component is empty. " +
                           "If the object has more than one Microgame Manager component, delete the extra one.", this);
            sessionBroken = true;
            yield break;
        }
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

        // Work out the time limit and listen for the result BEFORE starting the
        // microgame, so a result reported instantly inside StartMicrogame isn't missed.
        float timeLimit = activeMicrogame.IgnoreDifficultyRamp
            ? activeMicrogame.BaseTimeLimit
            : Mathf.Max(minimumTimeLimit, activeMicrogame.BaseTimeLimit * currentTimeLimitMultiplier);
        timeLimit = Mathf.Max(0.01f, timeLimit); // avoids dividing by zero
        float timeRemaining = timeLimit;
        bool resultReceived = false;
        bool won = false;

        void HandleResult(bool result)
        {
            resultReceived = true;
            won = result;
        }

        activeMicrogame.OnMicrogameResult += HandleResult;

        // Reset the bar to full at the start of each round
        if (timerSlider != null) timerSlider.value = 1f;

        // Run the actual microgame with a shrinking timer.
        chosenObject.SetActive(true);
        activeMicrogame.StartMicrogame();

        while (timeRemaining > 0f && !resultReceived)
        {
            timeRemaining -= Time.deltaTime;
            activeMicrogame.TickMicrogame(Time.deltaTime);

            if (timerSlider != null)
                timerSlider.value = Mathf.Clamp01(timeRemaining / timeLimit);

            yield return null;
        }

        activeMicrogame.OnMicrogameResult -= HandleResult;

        if (!resultReceived)
        {
            // Ran out of time without a reported result. For most microgames that is
            // a loss, but survival games (like the finale) count it as a win.
            activeMicrogame.ForceStop();
            won = activeMicrogame.TimeoutCountsAsWin;
        }

        chosenObject.SetActive(false);

        lastRoundWon = won;

        if (won)
        {
            if (countsTowardTotal) gamesClearedCount++;
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
        // Replaying one level: always the same microgame.
        if (practiceMicrogame != null) return practiceMicrogame;

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

        if (shuffledBag.Count == 0) return null; // nothing to pick from

        GameObject next = shuffledBag[0];
        shuffledBag.RemoveAt(0);
        return next;
    }

    private void UpdateLivesUI()
    {
        if (livesText != null)
            livesText.text = NoZeros($"LIVES: {currentLives}");
    }

    private void UpdateGameCountUI()
    {
        if (gameCountText != null)
            gameCountText.text = NoZeros($"{gamesClearedCount} / {totalGamesThisSession}");
    }

    private void EndSession()
    {
        // The finale only has to be beaten if it is enabled.
        bool clearedAll = gamesClearedCount >= totalGamesThisSession
            && (practiceMicrogame != null || !FinaleActive || finaleWon);

        // Win = cleared everything with lives left
        bool playerWon = clearedAll && currentLives > 0;

        if (resultsPanel != null)
        {
            resultsPanel.gameObject.SetActive(true);
            resultsPanel.alpha = 1f;
        }

        SetObjectsActive(winObjects, playerWon);
        SetObjectsActive(loseObjects, !playerWon);

        if (resultsSummaryText != null)
        {
            resultsSummaryText.text = NoZeros(clearedAll
                ? $"CLEARED ALL {totalGamesThisSession}!"
                : $"GAME OVER\n{gamesClearedCount} / {totalGamesThisSession} cleared");
        }
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (obj == null) continue;

            obj.SetActive(active);

            // When showing, also switch on every child (including ones that were disabled)
            if (active)
            {
                foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
                    child.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Optional "Play Again" button on the results panel. Reloads this scene, and
    /// since the chosen level is still saved, it replays the same level (or a
    /// fresh normal run if that is what you were playing).
    /// </summary>
    public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Hook this up to a "Back to Menu" button on the results panel.</summary>
    public void ReturnToMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}