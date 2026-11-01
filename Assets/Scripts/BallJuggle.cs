using System.Collections;
using UnityEngine;

public class BallJuggle : MicrogameBase
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private int ballNumber;
    [SerializeField] private Transform ballParent;
    [SerializeField] private float ballSpawnHeight;//width at which the ball will spawn
    [SerializeField] private float ballSpawnWidth;//height at which the ball will spawn

    [SerializeField] private GameObject paddle;
    [SerializeField] private Transform paddleParent;

    [Header("Play Area & Player")]
    [SerializeField] private RectTransform playArea;   // defines movement bounds AND receives touch input
    [SerializeField] private Canvas parentCanvas;       // needed to convert screen touch position correctly
    [SerializeField] private RectTransform playerShip;
    [SerializeField] private float playerFollowSpeed = 900f; // how quickly the ship catches up to your finger
    [SerializeField] private float playerHalfWidth = 30f;
    private GameObject paddleInstance;

    private float timeRemaining;
    private readonly float timeCheck = 0.1f;
    private MicrogameManager microgameManager;

    public override void StartMicrogame()
    {
        DestroyObstacles("BallJuggleBall");
        DestroyObstacles("BallJugglePaddle");
        paddleInstance = Instantiate(paddle);
        microgameManager = GameObject.Find("MicroGameManager").GetComponent<MicrogameManager>();
        paddleInstance.transform.SetParent(paddleParent, false);
        BallSpawn(ballNumber);
        StartCoroutine(Timer());
        base.StartMicrogame();
    }

    private void Update()
    {
        if (paddleInstance != null)
        {
            playerShip = paddleInstance.GetComponent<RectTransform>();
            HandlePlayerMovement(Time.deltaTime);
        }
    }
    private IEnumerator Timer()
    {
        float timeLimit = Mathf.Max(microgameManager.minimumTimeLimit, BaseTimeLimit * microgameManager.currentTimeLimitMultiplier);
        while (timeLimit > 0)
        {
            timeLimit -= Time.deltaTime;
            timeRemaining = timeLimit;
            yield return null;
        }
        if (timeRemaining <= timeCheck)
        {
            GameResult(true);
        }
    }
    private void BallSpawn(int number)
    {
        for (int i = 0; i < number; i++)
        {
            GameObject ball = Instantiate(ballPrefab);
            ball.transform.SetParent(ballParent, false);
            ball.transform.localPosition = new Vector3(Random.Range(-ballSpawnWidth, ballSpawnWidth), Random.Range(-ballSpawnHeight, ballSpawnHeight), 0);
        }
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
    private bool IsPointerDown(out Vector2 screenPosition)
    {
        // Touch takes priority so this behaves correctly on an actual device.
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
        }

        // Falls back to the mouse so you can fully test this in the Editor.
        if (Input.GetMouseButton(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        screenPosition = default;
        return false;
    }
    private void HandlePlayerMovement(float deltaTime)
    {
        if (playerShip == null || playArea == null) return;
        if (!IsPointerDown(out Vector2 screenPosition)) return;

        // Convert the raw screen touch position into a local X coordinate
        // inside PlayArea, so it lines up correctly regardless of Canvas
        // scaling or resolution.
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(playArea, screenPosition, cam, out Vector2 localPoint))
            return;

        Vector2 pos = playerShip.anchoredPosition;
        float targetX = localPoint.x;

        float halfWidth = playArea.rect.width / 2f - playerHalfWidth;
        targetX = Mathf.Clamp(targetX, -halfWidth, halfWidth);

        // Smoothly follow the touch instead of snapping, so the ship still
        // feels controllable rather than teleporting to your finger.
        pos.x = Mathf.MoveTowards(pos.x, targetX, playerFollowSpeed * deltaTime);
        playerShip.anchoredPosition = pos;
    }
    private void GameResult(bool won)
    {
        Debug.Log("Running Result Check");
        GameObject[] balls = GameObject.FindGameObjectsWithTag("BallJuggleBall");
        Debug.Log("Balls remaining: " + balls.Length);
        Debug.Log("Balls required: " + ballNumber);
        if (balls.Length == ballNumber)
        {

            ReportResult(won);
        }
    }
    public void FailState()
    {
        Debug.Log("player lost");
        ReportResult(false);
    }

    public override void ForceStop()
    {
        DestroyObstacles("BallJuggleBall");
        DestroyObstacles("BallJugglePaddle");
        base.ForceStop();
    }
}
