using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Microgame: keep every ball in play until the timer runs out.
/// The timer length is fixed (Base Time Limit) and does NOT shrink. Instead, higher
/// difficulty makes the balls faster, up to a maximum so it never becomes impossible.
/// </summary>
public class BallJuggle : MicrogameBase, IDifficultyAware
{
    [Header("Balls")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private int ballNumber = 3;
    [Tooltip("How far left/right of the center a ball can spawn.")]
    [SerializeField] private float ballSpawnWidth = 300f;
    [Tooltip("How tall the spawn strip is, measured down from the top of the Play Area (pixels).")]
    [SerializeField] private float ballSpawnHeight = 40f;
    [Tooltip("How far below the top edge of the Play Area balls spawn (pixels).")]
    [SerializeField] private float spawnTopMargin = 20f;
    [Tooltip("Shifts the spawn strip left or right. (Y is ignored; balls always spawn at the top.)")]
    [SerializeField] private Vector2 spawnCenterOffset = Vector2.zero;

    [Header("Spawn Variety")]
    [SerializeField] private float spawnTime = 0.4f;
    [SerializeField] private float spawnTimeVariation = 0.3f;
    [SerializeField] private float minBallSpacing = 80f;
    [SerializeField] private float startSidewaysSpeed = 150f;
    [SerializeField] private Vector2 startDownSpeedRange = new Vector2(0f, 100f);

    [Header("Difficulty (balls get faster)")]
    [Tooltip("How much faster the balls get per +1.0 of difficulty. 0.5 = 50% faster per full step.")]
    [SerializeField] private float speedPerDifficulty = 0.5f;
    [Tooltip("The balls never go more than this many times faster than normal.")]
    [SerializeField] private float maxSpeedMultiplier = 1.8f;
    [Tooltip("Hard cap on any ball's speed (pixels per second), so it can never become impossible.")]
    [SerializeField] private float maxBallSpeed = 1400f;

    [Header("Paddle")]
    [SerializeField] private GameObject paddle;
    [Tooltip("Optional. Leave empty to put the paddle inside the Play Area.")]
    [SerializeField] private Transform paddleParent;
    [SerializeField] private float paddleBounciness = 1f;
    [SerializeField] private float minPaddleBounceSpeed = 600f;
    [SerializeField] private float paddleSteer = 300f;
    [SerializeField] private float paddleBottomOffset = 120f;

    [Header("Losing")]
    [SerializeField] private float floorMargin = 0f;

    [Header("Play Area & Player")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private float playerFollowSpeed = 900f;
    [SerializeField] private float playerHalfWidth = 30f;

    [Header("Wall Bounce")]
    [SerializeField] private bool bounceOffSides = true;
    [SerializeField] private float sideBounciness = 1f;
    [SerializeField] private bool bounceOffTop = true;
    [SerializeField] private float topBounciness = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] bounceSFX;
    [Range(0f, 1f)] [SerializeField] private float bounceVolume = 1f;
    [SerializeField] private AudioClip[] paddleHitSFX;
    [Range(0f, 1f)] [SerializeField] private float paddleHitVolume = 1f;
    [Range(0f, 0.5f)] [SerializeField] private float pitchVariation = 0.1f;
    [SerializeField] private float bounceSoundCooldown = 0.05f;

    private readonly List<GameObject> balls = new List<GameObject>();
    private readonly Vector3[] corners = new Vector3[4];
    private GameObject paddleInstance;
    private RectTransform playerShip;
    private bool gameActive;
    private bool hasFailed;
    private float difficulty = 1f;
    private float lastBounceSoundTime = -10f;
    private float lastPaddleSoundTime = -10f;

    // Surviving until the timer runs out is the win.
    public override bool TimeoutCountsAsWin => !hasFailed;

    // The timer stays at Base Time Limit instead of shrinking as the run goes on.
    public override bool IgnoreDifficultyRamp => true;

    // Called by MicrogameManager just before the round starts.
    public void SetDifficulty(float value)
    {
        difficulty = Mathf.Max(1f, value);
    }

    // 1 = normal speed. Grows with difficulty, but never above the max.
    private float SpeedMultiplier =>
        Mathf.Min(maxSpeedMultiplier, 1f + (difficulty - 1f) * speedPerDifficulty);

    // ---------- Lifecycle ----------

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        hasFailed = false;
        gameActive = true;
        lastBounceSoundTime = -10f;
        lastPaddleSoundTime = -10f;

        StopAllCoroutines();
        ClearBalls();
        ClearPaddle();

        if (paddle != null)
        {
            paddleInstance = Instantiate(paddle);
            paddleInstance.transform.SetParent(paddleParent != null ? paddleParent : playArea, false);
            playerShip = paddleInstance.GetComponent<RectTransform>();

            if (playerShip != null && playArea != null)
            {
                playerShip.anchorMin = playerShip.anchorMax = new Vector2(0.5f, 0.5f);
                playerShip.anchoredPosition = new Vector2(
                    0f,
                    playArea.rect.yMin + paddleBottomOffset + playerShip.rect.height * playerShip.pivot.y);
            }
        }

        StartCoroutine(BallSpawn(ballNumber));
    }

    public override void ForceStop()
    {
        gameActive = false;
        StopAllCoroutines();
        ClearBalls();
        ClearPaddle();
        base.ForceStop();
    }

    private void OnDisable()
    {
        gameActive = false;
    }

    private void Update()
    {
        if (!gameActive) return;

        if (playerShip != null)
            HandlePlayerMovement(Time.deltaTime);

        UpdateBalls();
    }

    // ---------- Sound ----------

    public void PlayBounceSound()
    {
        if (Time.time - lastBounceSoundTime < bounceSoundCooldown) return;
        if (PlayRandomClip(bounceSFX, bounceVolume))
            lastBounceSoundTime = Time.time;
    }

    public void PlayPaddleHitSound()
    {
        if (paddleHitSFX == null || paddleHitSFX.Length == 0)
        {
            PlayBounceSound();
            return;
        }

        if (Time.time - lastPaddleSoundTime < bounceSoundCooldown) return;
        if (PlayRandomClip(paddleHitSFX, paddleHitVolume))
            lastPaddleSoundTime = Time.time;
    }

    private bool PlayRandomClip(AudioClip[] clips, float volume)
    {
        if (!gameActive || audioSource == null) return false;
        if (clips == null || clips.Length == 0) return false;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return false;

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, volume);
        return true;
    }

    // ---------- Spawning ----------

    private IEnumerator BallSpawn(int number)
    {
        for (int i = 0; i < number; i++)
        {
            SpawnBall();

            if (i < number - 1)
            {
                // Spawn gaps shrink with speed too, so the whole round feels faster.
                float wait = (spawnTime + Random.Range(-spawnTimeVariation, spawnTimeVariation)) / SpeedMultiplier;
                if (wait > 0f) yield return new WaitForSeconds(wait);
            }
        }
    }

    private void SpawnBall()
    {
        if (ballPrefab == null || playArea == null) return;

        GameObject ball = Instantiate(ballPrefab);
        ball.transform.SetParent(playArea, false);
        ball.transform.localPosition = PickSpawnPosition();
        balls.Add(ball);

        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float m = SpeedMultiplier;

            // Speeding everything up by m means velocities x m and gravity x m squared,
            // so the ball follows the same path, just faster.
            rb.gravityScale *= m * m;

            float sideways = Random.Range(-startSidewaysSpeed, startSidewaysSpeed) * m;
            float down = -Random.Range(startDownSpeedRange.x, startDownSpeedRange.y) * m;
            SetVelocity(rb, new Vector2(sideways, down));
        }
        else
        {
            Debug.LogError("BallJuggle: the Ball Prefab needs a Rigidbody2D (Body Type Dynamic) so it can fall.");
        }
    }

    private Vector3 PickSpawnPosition()
    {
        float halfBall = 25f;
        RectTransform ballRect = ballPrefab != null ? ballPrefab.transform as RectTransform : null;
        if (ballRect != null) halfBall = ballRect.rect.height * 0.5f;

        float topY = playArea.rect.yMax - spawnTopMargin - halfBall;
        float minY = topY - ballSpawnHeight;

        float paddleTop = playerShip != null ? LocalRect(playerShip).yMax : playArea.rect.yMin;
        minY = Mathf.Max(minY, paddleTop + halfBall * 2f);
        if (minY > topY) minY = topY;

        float halfWidth = Mathf.Min(ballSpawnWidth, playArea.rect.width * 0.5f - halfBall);

        Vector3 best = new Vector3(0f, topY, 0f);
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 candidate = new Vector3(
                spawnCenterOffset.x + Random.Range(-halfWidth, halfWidth),
                Random.Range(minY, topY),
                0f);
            candidate.x = Mathf.Clamp(candidate.x, -halfWidth, halfWidth);

            float nearest = float.MaxValue;
            foreach (GameObject other in balls)
            {
                if (other == null) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(candidate, other.transform.localPosition));
            }

            if (nearest >= minBallSpacing) return candidate;

            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = candidate;
            }
        }

        return best;
    }

    private void ClearBalls()
    {
        foreach (GameObject ball in balls)
            if (ball != null) Destroy(ball);
        balls.Clear();
    }

    private void ClearPaddle()
    {
        if (paddleInstance != null) Destroy(paddleInstance);
        paddleInstance = null;
        playerShip = null;
    }

    // ---------- Per-frame ball logic: sides, top, paddle, floor ----------

    private void UpdateBalls()
    {
        if (playArea == null) return;

        float m = SpeedMultiplier;
        float minX = playArea.rect.xMin;
        float maxX = playArea.rect.xMax;
        float maxY = playArea.rect.yMax;
        float floorY = playArea.rect.yMin - floorMargin;

        bool hasPaddle = playerShip != null;
        Rect paddleRect = hasPaddle ? LocalRect(playerShip) : default;

        for (int i = balls.Count - 1; i >= 0; i--)
        {
            GameObject ball = balls[i];
            if (ball == null)
            {
                balls.RemoveAt(i);
                continue;
            }

            Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
            RectTransform rt = ball.transform as RectTransform;
            if (rb == null || rt == null) continue;

            Vector3 local = playArea.InverseTransformPoint(rt.position);
            float scaleRatio = playArea.lossyScale.x != 0f ? rt.lossyScale.x / playArea.lossyScale.x : 1f;
            float halfW = rt.rect.width * 0.5f * scaleRatio;
            float halfH = rt.rect.height * 0.5f * scaleRatio;

            Vector2 velocity = GetVelocity(rb);
            bool changed = false;
            bool wallBounced = false;
            bool paddleHit = false;

            if (bounceOffSides)
            {
                if (local.x - halfW < minX && velocity.x < 0f)
                {
                    local.x = minX + halfW;
                    velocity.x = -velocity.x * sideBounciness;
                    changed = wallBounced = true;
                }
                else if (local.x + halfW > maxX && velocity.x > 0f)
                {
                    local.x = maxX - halfW;
                    velocity.x = -velocity.x * sideBounciness;
                    changed = wallBounced = true;
                }
            }

            if (bounceOffTop && local.y + halfH > maxY && velocity.y > 0f)
            {
                local.y = maxY - halfH;
                velocity.y = -velocity.y * topBounciness;
                changed = wallBounced = true;
            }

            if (hasPaddle && velocity.y < 0f && local.y > paddleRect.center.y)
            {
                bool overlapX = local.x + halfW > paddleRect.xMin && local.x - halfW < paddleRect.xMax;
                bool overlapY = local.y - halfH < paddleRect.yMax && local.y + halfH > paddleRect.yMin;

                if (overlapX && overlapY)
                {
                    local.y = paddleRect.yMax + halfH;

                    velocity.y = Mathf.Max(-velocity.y * paddleBounciness, minPaddleBounceSpeed * m);

                    float halfPaddle = Mathf.Max(1f, paddleRect.width * 0.5f);
                    float offset = Mathf.Clamp((local.x - paddleRect.center.x) / halfPaddle, -1f, 1f);
                    velocity.x += offset * paddleSteer * m;

                    changed = paddleHit = true;
                }
            }

            // Speed limit: no ball ever goes faster than this, however high the difficulty.
            if (velocity.magnitude > maxBallSpeed)
            {
                velocity = velocity.normalized * maxBallSpeed;
                changed = true;
            }

            if (changed)
            {
                rt.position = playArea.TransformPoint(local);
                SetVelocity(rb, velocity);
            }

            if (paddleHit) PlayPaddleHitSound();
            else if (wallBounced) PlayBounceSound();

            if (local.y + halfH < floorY)
            {
                FailState();
                return;
            }
        }
    }

    private Rect LocalRect(RectTransform rt)
    {
        rt.GetWorldCorners(corners);
        Vector3 min = playArea.InverseTransformPoint(corners[0]);
        Vector3 max = playArea.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Vector2 GetVelocity(Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody2D rb, Vector2 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }

    // ---------- Player ----------

    private bool IsPointerDown(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
        }

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

        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(playArea, screenPosition, cam, out Vector2 localPoint))
            return;

        Vector2 pos = playerShip.anchoredPosition;
        float halfWidth = playArea.rect.width / 2f - playerHalfWidth;
        float targetX = Mathf.Clamp(localPoint.x, -halfWidth, halfWidth);

        pos.x = Mathf.MoveTowards(pos.x, targetX, playerFollowSpeed * deltaTime);
        playerShip.anchoredPosition = pos;
    }

    // ---------- Losing ----------

    public void FailState()
    {
        if (!gameActive || hasFailed) return;

        hasFailed = true;
        gameActive = false;
        ReportResult(false);
    }
}