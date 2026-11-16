using UnityEngine;

public class CloseInstructions : MonoBehaviour
{
    private MicrogameManager microgameManager;

    public void CloseInstructionBox()
    {
        microgameManager = GameObject.Find("MicroGameManager").GetComponent<MicrogameManager>();
        microgameManager.HideMicrogameInstructions();
    }
}
