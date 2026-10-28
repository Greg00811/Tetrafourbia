using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public GameObject enemyOnePrefab;

    public GameObject enemyTwoPrefab;

    public GameObject enemyThreePrefab;

    public TextMeshProUGUI livesText;

    // Added from old script
    public GameObject gameOverText;
    public GameObject restartText;

    public int score;

    public float horizontalScreenSize = 6f;

    public float verticalScreenSize = 5f;

    // Added from old script
    private bool gameOver;

    // Start is called before the first frame update
    void Start()
    {
        score = 0;
        gameOver = false;

        InvokeRepeating("CreateEnemyOne", 1, 2);
        InvokeRepeating("CreateEnemyTwo", 3, 4);
        InvokeRepeating("CreateEnemyThree", 5, 6);
    }

    // Added from old script
    void Update()
    {
        if (gameOver && Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void CreateEnemyOne()
    {
        Debug.Log("I am enemy one");
        Instantiate(enemyOnePrefab, new Vector3(Random.Range(-9f, 9f), 6.5f, 0), Quaternion.identity);
    }

    void CreateEnemyTwo()
    {
        Debug.Log("I am enemy two");
        Instantiate(enemyTwoPrefab, new Vector3(Random.Range(-7f, 7f), 6.5f, 0), Quaternion.identity);
    }

    void CreateEnemyThree()
    {
        Debug.Log("I am enemy three");
        Instantiate(enemyThreePrefab, new Vector3(Random.Range(-3f, 3f), 6.5f, 0), Quaternion.identity);
    }

    public void AddScore(int earnedScore)
    {
        score += earnedScore;
    }

    public void ChangeLivesText(int currentLives)
    {
        livesText.text = "Lives: " + currentLives;
    }

    // Added from old script
    public void GameOver()
    {
        gameOverText.SetActive(true);
        restartText.SetActive(true);
        gameOver = true;
        CancelInvoke();
    }
}
