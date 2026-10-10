using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Plays a NarrativeSequence as tap-to-advance dialogue with an NPC sprite.
///
/// How a line plays:
///   1. Text types out character by character while the NPC sprite bobs,
///      so it looks like they're talking.
///   2. Tapping while it's still typing instantly finishes the line.
///   3. A "tap to continue" indicator appears; tapping again moves on.
///
/// Uses a full-screen UI Button for the tap, so it works with touch AND mouse
/// clicks and doesn't depend on the old UnityEngine.Input class.
///
/// IMPORTANT: put THIS script on an object that stays active (e.g. an empty
/// "DialogueSystem" object). The panel it shows/hides is a separate child
/// object assigned to "Dialogue Root" - don't put the script on that panel
/// itself, or it can't run while the panel is hidden.
///
/// Called from MicrogameManager like:
///     yield return StartCoroutine(dialoguePlayer.Play(someSequence));
/// </summary>
public class DialoguePlayer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogueRoot;      // the full-screen panel that gets shown/hidden
    [SerializeField] private Button advanceButton;          // full-screen Button covering the panel (the tap target)
    [SerializeField] private Image npcImage;                // the NPC portrait/sprite
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject tapIndicator;       // small "tap to continue" arrow/text, hidden while typing

    [Header("Typing")]
    [SerializeField] private float charactersPerSecond = 40f;

    [Header("Talking Animation (NPC bobs while text types)")]
    [SerializeField] private float bobHeight = 12f;
    [SerializeField] private float bobSpeed = 18f;

    private bool tapRequested;
    private Vector2 npcRestPosition;

    private void Awake()
    {
        if (advanceButton != null)
            advanceButton.onClick.AddListener(OnTap);

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    private void OnTap()
    {
        tapRequested = true;
    }

    /// <summary>
    /// Plays every line in the sequence, then hides the panel and returns.
    /// Safe to call with a null/empty sequence (it just returns immediately).
    /// </summary>
    public IEnumerator Play(NarrativeSequence sequence)
    {
        if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
            yield break;

        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (npcImage != null) npcRestPosition = npcImage.rectTransform.anchoredPosition;

        foreach (DialogueLine line in sequence.lines)
        {
            // Sprite and speaker: use per-line override if set, else the defaults.
            Sprite sprite = line.npcSprite != null ? line.npcSprite : sequence.defaultNpcSprite;
            string speaker = !string.IsNullOrEmpty(line.speakerName) ? line.speakerName : sequence.defaultSpeakerName;

            if (npcImage != null)
            {
                npcImage.sprite = sprite;
                npcImage.preserveAspect = true;
                npcImage.enabled = sprite != null;
            }
            if (speakerNameText != null) speakerNameText.text = speaker;

            if (tapIndicator != null) tapIndicator.SetActive(false);
            tapRequested = false;

            yield return StartCoroutine(TypeLine(line.text));

            // Line fully shown - wait for a tap to move on.
            if (tapIndicator != null) tapIndicator.SetActive(true);
            tapRequested = false;
            while (!tapRequested)
                yield return null;
        }

        if (tapIndicator != null) tapIndicator.SetActive(false);
        if (npcImage != null) npcImage.rectTransform.anchoredPosition = npcRestPosition;
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
    }

    private IEnumerator TypeLine(string fullText)
    {
        if (dialogueText == null) yield break;

        dialogueText.text = fullText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate(); // so characterCount is accurate (ignores rich-text tags)

        int totalCharacters = dialogueText.textInfo.characterCount;
        float visibleCharacters = 0f;

        while (visibleCharacters < totalCharacters)
        {
            // A tap mid-line skips straight to the end of the line.
            if (tapRequested)
            {
                tapRequested = false;
                break;
            }

            visibleCharacters += charactersPerSecond * Time.deltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(visibleCharacters));

            // Talking bob.
            if (npcImage != null)
            {
                float offset = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight;
                npcImage.rectTransform.anchoredPosition = npcRestPosition + new Vector2(0f, offset);
            }

            yield return null;
        }

        dialogueText.maxVisibleCharacters = totalCharacters;
        if (npcImage != null) npcImage.rectTransform.anchoredPosition = npcRestPosition;
    }
}
