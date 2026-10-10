using UnityEngine;

/// <summary>
/// One line of dialogue. Leave the sprite/name empty to use the sequence's
/// defaults, or fill them in to swap the portrait/speaker for just this line
/// (e.g. an angry sprite for a single line).
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("Leave blank to use the sequence's default speaker name.")]
    public string speakerName;

    [TextArea(2, 5)]
    public string text;

    [Tooltip("Optional. Leave empty to use the sequence's default NPC sprite.")]
    public Sprite npcSprite;
}

/// <summary>
/// A single story beat: an ordered list of lines spoken by an NPC.
/// Create one via Assets -> Create -> Microgame -> Narrative Sequence, fill in
/// the lines, then drag it into MicrogameManager's intro/interlude slots.
/// </summary>
[CreateAssetMenu(fileName = "NewNarrativeSequence", menuName = "Microgame/Narrative Sequence")]
public class NarrativeSequence : ScriptableObject
{
    [Header("Defaults used by any line that leaves these blank")]
    public string defaultSpeakerName = "???";
    public Sprite defaultNpcSprite;

    [Header("Lines (played in order, one tap per line)")]
    public DialogueLine[] lines;
}
