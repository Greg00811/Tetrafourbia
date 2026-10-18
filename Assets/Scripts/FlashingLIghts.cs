using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Microgame: "Flashing Lights"
/// 
/// 4 lights are shown to the player.
/// The player must repeat the sequence of lights that flash in the same order.
/// Missing any of these lights results in a failure.
/// 
/// IMPORTANT
/// BASE TIME LIMIT IS INCREASED TO 8 SECONDS TO ACCOUNT FOR THE FLASHING LIGHTS.
/// 
/// More colors can be added in the inspector, but make sure to set the lights' Alpha to 0.5, as to match the other colors present.
public class FlashingLights : MicrogameBase
{
    [Header("Lights Setup")]
    [SerializeField] private Button[] lights;
    //make sure to make the lights' Alpha 1 and not 0, otherwise the color won't be visible
    [SerializeField] private Color[] lightColors;
    [SerializeField] private int lightsChain; //number of lights that need to be memorized
    [SerializeField] private float flashTime; //time that each light is shown to the player

    private HashSet<Color> usedColors = new();
    private List<Button> buttonOrder = new();
    private List<Button> playerOrder = new();
    public override void StartMicrogame()
    {
        ColorPicker();
        ButtonOrder();
        StartCoroutine(ShowPatternRoutine());
        foreach (Button light in lights)
        {
            light.onClick.RemoveAllListeners();
            light.onClick.AddListener(() => HandleLightClick(light));
        }
        base.StartMicrogame();
    }

    private void ButtonOrder()
    {
        for (int i = 0; i < lightsChain; i++)
        {
            int randomIndex = Random.Range(0, lights.Length);
            buttonOrder.Add(lights[randomIndex]);
            Debug.Log(lights[randomIndex] + " added to button order");
        }
    }
    private void ColorPicker()
    {
        foreach (Button light in lights)
        {
            int randomIndex = Random.Range(0, lightColors.Length);
            Color randomColor = lightColors[randomIndex];
            if (usedColors.Contains(randomColor))
            {
                // If the color has already been used, pick a new one
                while (usedColors.Contains(randomColor))
                {
                    randomIndex = Random.Range(0, lightColors.Length);
                    randomColor = lightColors[randomIndex];
                }
            }
            usedColors.Add(randomColor);
            light.GetComponent<Image>().color = randomColor;
            Debug.Log(light + "light color set to: " + randomColor);
        }
    }

    private IEnumerator ShowPatternRoutine()
    {
        // Optional short delay before the pattern starts playing
        yield return new WaitForSeconds(0.5f);

        foreach (Button patternLight in buttonOrder)
        {
            Image img = patternLight.GetComponent<Image>();
            Color originalColor = img.color;

            // Make it bright/fully visible
            Color flashColor = originalColor;
            flashColor.a = 1f;
            img.color = flashColor;

            Debug.Log(patternLight.name + " shown to player.");

            // Wait for the duration of the flash
            //yield return new WaitForSeconds(flashTime * GameObject.Find("MicroGameManager").GetComponent<MicrogameManager>().speedRampMultiplier);
            //cant access the speedRampMultiplier because it is private
            yield return new WaitForSeconds(flashTime);
            // Revert back to semi-transparent / dim state
            Color dimColor = originalColor;
            dimColor.a = 0.5f;
            img.color = dimColor;

            // Small gap between flashes so they don't blend together if it's the same button twice
            yield return new WaitForSeconds(0.2f);

            // Return to original color state
            img.color = originalColor;

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void HandleLightClick(Button clickedLight)
    {
        playerOrder.Add(clickedLight);
        Debug.Log(clickedLight.name + " clicked by player.");
        // Check if the player's input matches the expected pattern so far
        for (int i = 0; i < playerOrder.Count; i++)
        {
            if (playerOrder[i] != buttonOrder[i])
            {
                Debug.Log("Player clicked the wrong light. Game Over.");
                ReportResult(false); // Player clicked the wrong light
                return;
            }
        }
        // If the player has completed the entire sequence correctly
        if (playerOrder.Count == buttonOrder.Count)
        {
            Debug.Log("Player completed the sequence correctly. You Win!");
            ReportResult(true); // Player completed the sequence correctly
        }
    }
    public override void ForceStop()
    {
        base.ForceStop();
    }
}
