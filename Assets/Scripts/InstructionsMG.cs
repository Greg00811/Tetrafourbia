using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InstructionsMG : MonoBehaviour
{
    [SerializeField] private TMP_Text instructionsText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text additionalText;
    public void DisplayInstructions(string instructions)
    {
        instructionsText.text = instructions;
        Time.timeScale = 0f; // Pause the game
    }

    public void DisplayName(string mgName)
    {
        nameText.text = mgName;
    }

    public void DisplayAdditionalText(string addText)
    {
        additionalText.text = addText;
    }
}
