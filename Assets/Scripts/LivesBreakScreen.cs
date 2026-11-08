using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LivesBreakScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image[] guys;          // the 4 Images already on screen, left to right
    [SerializeField] private Sprite[] deadSprites;  // 4 dead sprites, same order as guys
    [SerializeField] private TMP_Text livesCountText;
    [SerializeField] private TMP_Text resultText;

    [Header("Text")]
    [SerializeField] private string winMessage = "NICE!";
    [SerializeField] private string loseMessage = "OUCH!";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip loseSound;

    [Header("Lose Frames")]
    [SerializeField] private Sprite[] loseFrames;           // the frames that play
    [SerializeField] private float loseAnimFps = 12f;
    [SerializeField] private float loseAnimDelay = 0.5f;    // delay before the frames start
    [SerializeField] private float loseAnimScale = 1f;      // raise this to make the lose frames bigger
    [SerializeField] private Vector2 loseAnimOffset = Vector2.zero; // nudge position relative to the guy
    [SerializeField] private int swapToDeadOnFrame = 8;     // 1-based: guy becomes dead on this frame

    [Header("Timing (seconds)")]
    [SerializeField] private float introPause = 0.35f;
    [SerializeField] private float holdTime = 0.9f;

    private Image fxImage;   // created automatically the first time it's needed

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    public IEnumerator Show(int livesRemaining, bool lostLife, bool wonRound)
    {
        if (root == null) yield break;

        root.SetActive(true);

        if (resultText != null)
            resultText.text = wonRound ? winMessage : loseMessage;

        if (audioSource != null)
        {
            AudioClip clip = wonRound ? winSound : loseSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        int lostIndex = livesRemaining;
        int startAliveCount = lostLife ? livesRemaining + 1 : livesRemaining;

        // Alive guys keep dancing. Already-dead guys stay on their dead sprite.
        for (int i = 0; i < guys.Length; i++)
        {
            if (guys[i] == null) continue;

            if (i < startAliveCount)
                SetAnimatorEnabled(i, true);
            else
                SetDead(i);
        }

        SetCountText(startAliveCount);
        yield return new WaitForSeconds(introPause);

        if (lostLife && lostIndex >= 0 && lostIndex < guys.Length && guys[lostIndex] != null)
        {
            yield return new WaitForSeconds(loseAnimDelay);
            yield return StartCoroutine(PlayLoseFrames(lostIndex));
        }

        SetCountText(livesRemaining);
        yield return new WaitForSeconds(holdTime);

        root.SetActive(false);
    }

    private IEnumerator PlayLoseFrames(int guyIndex)
    {
        if (loseFrames == null || loseFrames.Length == 0)
        {
            SetDead(guyIndex);
            yield break;
        }

        Image fx = GetFxImage(guyIndex);
        fx.gameObject.SetActive(true);

        int swapIndex = Mathf.Clamp(swapToDeadOnFrame - 1, 0, loseFrames.Length - 1);
        float frameTime = 1f / Mathf.Max(1f, loseAnimFps);

        for (int f = 0; f < loseFrames.Length; f++)
        {
            fx.sprite = loseFrames[f];
            fx.SetNativeSize();   // size matches the frame, then scale is applied on top

            // The guy keeps his current sprite until this frame
            if (f == swapIndex)
                SetDead(guyIndex);

            yield return new WaitForSeconds(frameTime);
        }

        fx.gameObject.SetActive(false);
    }

    private Image GetFxImage(int guyIndex)
    {
        RectTransform guyRect = guys[guyIndex].rectTransform;

        if (fxImage == null)
        {
            GameObject go = new GameObject("LoseFramesFX", typeof(RectTransform), typeof(Image));
            fxImage = go.GetComponent<Image>();
            fxImage.raycastTarget = false;
            fxImage.preserveAspect = true;
        }

        RectTransform fx = fxImage.rectTransform;
        fx.SetParent(guyRect.parent, false);
        fx.anchorMin = fx.anchorMax = new Vector2(0.5f, 0.5f);
        fx.pivot = new Vector2(0.5f, 0.5f);
        fx.SetAsLastSibling();                 // draw on top of the guys
        fx.position = guyRect.position;        // center on the guy
        fx.anchoredPosition += loseAnimOffset;
        fx.localScale = Vector3.one * loseAnimScale;

        return fxImage;
    }

    /// <summary>Call this when a new run starts so everyone dances again.</summary>
    public void ResetGuys()
    {
        for (int i = 0; i < guys.Length; i++)
            SetAnimatorEnabled(i, true);
    }

    private void SetDead(int i)
    {
        SetAnimatorEnabled(i, false);
        if (deadSprites != null && i < deadSprites.Length && deadSprites[i] != null)
            guys[i].sprite = deadSprites[i];
    }

    private void SetAnimatorEnabled(int i, bool enabled)
    {
        if (guys[i] == null) return;
        Animator anim = guys[i].GetComponent<Animator>();
        if (anim != null) anim.enabled = enabled;
    }

    private void SetCountText(int lives)
    {
        if (livesCountText == null) return;
        livesCountText.text = lives == 1 ? "1 LIFE LEFT" : $"{lives} LIVES LEFT";
    }
}