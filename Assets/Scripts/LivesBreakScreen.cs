using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The short "how many lives do I have left?" screen shown between microgames,
/// WarioWare-style. It auto-advances (no tapping needed) so the pace stays fast.
///
/// Shows one icon per life. If the player just lost a life, the screen first
/// shows the icon as still full, then pops it and fades it out so the loss is
/// easy to notice.
///
/// IMPORTANT: put THIS script on an object that stays active (e.g. an empty
/// "LivesBreakSystem" object). The visible panel is a separate child object
/// assigned to "Root" - the script hides/shows that, so it must not live on it.
///
/// Called from MicrogameManager like:
///     yield return StartCoroutine(livesBreakScreen.Show(lives, lostLife, wonRound));
/// </summary>
public class LivesBreakScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject root;            // the full-screen panel that gets shown/hidden
    [SerializeField] private Image[] lifeIcons;          // one Image per starting life (4), left to right
    [SerializeField] private TMP_Text livesCountText;    // e.g. "3 LIVES LEFT"
    [SerializeField] private TMP_Text resultText;        // e.g. "NICE!" / "OUCH!"

    [Header("Text")]
    [SerializeField] private string winMessage = "NICE!";
    [SerializeField] private string loseMessage = "OUCH!";

    [Header("Look")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color lostColor = new Color(1f, 1f, 1f, 0.2f);

    [Header("Timing (seconds)")]
    [SerializeField] private float introPause = 0.35f;     // how long the starting state is shown
    [SerializeField] private float lostLifeAnimTime = 0.5f; // length of the pop-and-fade
    [SerializeField] private float holdTime = 0.9f;         // pause at the end before the next round

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// Shows the screen, plays the lost-life animation if needed, then hides it.
    /// livesRemaining is the count AFTER the round that just finished.
    /// </summary>
    public IEnumerator Show(int livesRemaining, bool lostLife, bool wonRound)
    {
        if (root == null) yield break;

        root.SetActive(true);

        if (resultText != null)
            resultText.text = wonRound ? winMessage : loseMessage;

        // If a life was just lost, start by showing it as still there,
        // then animate it away. Otherwise just show the current state.
        int lostIconIndex = livesRemaining; // the icon that is about to disappear
        int startAliveCount = lostLife ? livesRemaining + 1 : livesRemaining;

        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] == null) continue;
            lifeIcons[i].color = i < startAliveCount ? aliveColor : lostColor;
            lifeIcons[i].rectTransform.localScale = Vector3.one;
        }

        SetCountText(startAliveCount);
        yield return new WaitForSeconds(introPause);

        if (lostLife && lostIconIndex >= 0 && lostIconIndex < lifeIcons.Length && lifeIcons[lostIconIndex] != null)
        {
            yield return StartCoroutine(AnimateLostLife(lifeIcons[lostIconIndex]));
        }

        SetCountText(livesRemaining);
        yield return new WaitForSeconds(holdTime);

        root.SetActive(false);
    }

    private IEnumerator AnimateLostLife(Image icon)
    {
        RectTransform rt = icon.rectTransform;
        float t = 0f;

        while (t < lostLifeAnimTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / lostLifeAnimTime);

            // Quick pop up, then shrink back down while fading to the "lost" color.
            float scale = p < 0.3f
                ? Mathf.Lerp(1f, 1.5f, p / 0.3f)
                : Mathf.Lerp(1.5f, 1f, (p - 0.3f) / 0.7f);

            rt.localScale = Vector3.one * scale;
            icon.color = Color.Lerp(aliveColor, lostColor, p);
            yield return null;
        }

        rt.localScale = Vector3.one;
        icon.color = lostColor;
    }

    private void SetCountText(int lives)
    {
        if (livesCountText == null) return;
        livesCountText.text = lives == 1 ? "1 LIFE LEFT" : $"{lives} LIVES LEFT";
    }
}
