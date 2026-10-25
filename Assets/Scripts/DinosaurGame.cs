using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Microgame: "Obstacle Jumping"
/// 
/// 4 obstacles are sent to the player in a slightly randomized order.
/// The player must jump over them with a button.
/// Colliding with any of the obstacles results in a failure.
/// 
/// [IMPORTANT]
/// Obstacle behavior script is located in Assets>MiniGameAssets>DinosaurGame


public class DinosaurGame : MicrogameBase
{
    [Header("Obstacle Settings")]
    [SerializeField] private float spawnTimerMin;
    [SerializeField] private float spawnTimerMax;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private Transform obstacleParent;
    private int obstaclesCount;
    public int obstaclesDestroyed;
    private Coroutine spawnCoroutine;

    [Header("Player Settings")]
    [SerializeField] private bool playerDead;
    [SerializeField] private Button jumpButton;
    [SerializeField] private float jumpTime;
    [SerializeField] private float jumpHeight; //recommended to be at least 1.25x the size of the object being jumped over
    private bool playerGrounded;
    private RectTransform playerTransform;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerParent;




    public override void StartMicrogame()
    {
        GameObject player = Instantiate(playerPrefab);
        player.transform.SetParent(playerParent, false);

        playerTransform = player.GetComponent<RectTransform>();
        playerDead = false;
        playerGrounded = true;
        obstaclesCount = 0;
        obstaclesDestroyed = 0;
        jumpButton.onClick.RemoveAllListeners();
        jumpButton.onClick.AddListener(() => HandleJumpClick(jumpButton));
        spawnCoroutine = StartCoroutine(SpawnObstacles());
        base.StartMicrogame();
    }

    private void Update()
    {
        if (playerDead == true)
        {
            StopCoroutine(spawnCoroutine);
            ReportResult(false);
            ForceStop();
        }
        if (obstaclesCount == 4)
        {
            StopCoroutine(spawnCoroutine);
        }
        if (obstaclesDestroyed == 4 && playerDead == false)
        {
            ReportResult(true);

        }
    }

    private IEnumerator SpawnObstacles()
    {
        while (playerDead == false)
        {
            GameObject obstacle = Instantiate(obstaclePrefab);
            obstacle.transform.SetParent(obstacleParent, false);
            obstaclesCount++;
            yield return new WaitForSeconds(Random.Range(spawnTimerMin, spawnTimerMax));
        }
    }

    private void HandleJumpClick(Button button)
    {
        if (playerGrounded == true)
        {
            playerGrounded = false;
            Debug.Log("Jumping");
            Vector2 pos = playerTransform.anchoredPosition;
            pos.y += jumpHeight;
            playerTransform.anchoredPosition = pos;
            StartCoroutine (PlayerDown());
        }
    }

    private IEnumerator PlayerDown()
    {
        yield return new WaitForSeconds(jumpTime);
        Vector2 pos = playerTransform.anchoredPosition;
        pos.y -= jumpHeight;
        playerTransform.anchoredPosition = pos;
        playerGrounded = true;
        Debug.Log("Landing");
    }

    private void DestroyObstacles(string tag)
    {
        GameObject[] obstacles = GameObject.FindGameObjectsWithTag(tag);

        for (int i = 0; i < obstacles.Length; i++)
        {
            Destroy(obstacles[i]);
        }
        Debug.Log(tag + "Obstacles destroyed");
    }
    public void FailState()
    {
        Debug.Log("player lost");
        playerDead = true;
    }
    public override void ForceStop()
    {
        DestroyObstacles("DinosaurGameObstacle");
        DestroyObstacles("DinosaurGamePlayer");
        base.ForceStop();
    }
}
