using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseFunctionality : MonoBehaviour
{

    private MicrogameManager microgameManager;
    [SerializeField] private GameObject pauseMenu;

    private void Start()
    {
        microgameManager = GameObject.Find("MicroGameManager").GetComponent<MicrogameManager>();
    }

    //assignable function to button taps to pause and unpause the game.
    //as long as the button has has been assigned this function it will in turn pause and unpause the game
    //for example, if the corner pause button is tapped the game will pause, and if a second button presents itself that says "resume" and it has the exact same functionality as the first button it will unpause the game. 
    public void PauseGame()
    {
        if (microgameManager.gamePaused == false)
        {
            Time.timeScale = 0f;
            microgameManager.gamePaused = true;
            if (pauseMenu != null && pauseMenu.activeSelf == false)
            {
                pauseMenu.SetActive(true);
            }
        }
        else if(microgameManager.gamePaused == true)
        {
            Time.timeScale = 1f;
            microgameManager.gamePaused = false;
            if (pauseMenu != null && pauseMenu.activeSelf == true)
            {
                pauseMenu.SetActive(false);
            }
        }
    }
    

}
