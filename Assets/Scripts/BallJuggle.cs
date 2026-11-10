using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Microgame: keep every ball in play until the timer runs out.
/// The MicrogameManager owns the timer. If it runs out and FailState()
/// was never called, the round counts as a WIN (TimeoutCountsAsWin).
/// Balls spawn in a strip along the TOP of the Play Area, fall with Rigidbody2D
/// gravity, bounce off the sides and top, bounce off the paddle, and lose the
/// round if one falls out the bottom. Paddle bounce and the floor are handled
/// in code (no colliders needed).
/// </summary>
public class BallJuggle : MicrogameBase
{
    [Header("Balls")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private int ballNumber = 3;
    [Tooltip("How far left/right of the center a ball can spawn.")]
    [SerializeField] private float ballSpawnWidth = 300f;
    [Tooltip("How tall the spawn strip is, measured down from the top of the Play Area (pixels).")]
    [SerializeField] private float ballSpawnHeight = 40f;
    [Tooltip("How far below the top edge of the Play Area balls spawn (pixels). Keeps them at the top, away from the paddle.")]
    [SerializeField] private float spawnTopMargin = 20f;
    [Tooltip("Shifts the spawn strip left or right. (Y is ignored; balls always spawn at the top.)")]
    [SerializeField] private Vector2 spawnCenterOffset = Vector2.zero;

    [Header("Spawn Variety")]
    [SerializeField] private float spawnTime = 0.4f;
    [SerializeField] private float spawnTimeVariation = 0.3f;
    [SerializeField] private float minBallSpacing = 80f;
    [SerializeField] private float startSidewaysSpeed = 150f;
    [SerializeField] private Vector2 startDownSpeedRange = new Vector2(0f, 100f);

    [Header("Paddle")]
    [SerializeField] private GameObject paddle;
    [Tooltip("Optional. Leave empty to put the paddle inside the Play Area.")]
    [SerializeField] private Transform paddleParent;
    [Tooltip("1 = ball keeps its speed when it bounces up off the paddle.")]
    [SerializeField] private float paddleBounciness = 1f;
    [Tooltip("The ball always bounces up at least this fast (pixels per second).")]
    [SerializeField] private float minPaddleBounceSpeed = 600f;
    [Tooltip("Hitting the paddle off-center pushes the ball sideways by up to this much.")]
    [SerializeField] private float paddleSteer = 300f;
    [Tooltip("How far above the bottom of the Play Area the paddle sits (pixels). Lower = closer to the bottom.")]
    [SerializeField] private float paddleBottomOffset = 120f;

    [Header("Losing")]
    [Tooltip("A ball counts as lost once it is this many pixels below the bottom of the Play Area.")]
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
    [Tooltip("1 = keeps all its speed bouncing off the top, below 1 = loses some.")]
    [SerializeField] private float topBounciness = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Plays when a ball hits the left, right or top wall. A random clip is used each time.")]
    [SerializeField] private AudioClip[] bounceSFX;
    [Range(0f, 1f)] [SerializeField] private float bounceVolume = 1f;
    [Tooltip("Plays when a ball hits the paddle. If empty, the wall bounce sound is used instead.")]
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
    private float lastBounceSoundTime = -10f;
    private float lastPaddleSoundTime = -10f;

    // Surviving until the manager's timer runs out is the win.
    public override bool TimeoutCountsAsWin => !hasFailed;

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
                // Anchor to the center and place the paddle a set distance above the bottom edge.
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

    // Wall bounce sound (left, right, top).
    public void PlayBounceSound()
    {
        if (Time.time - lastBounceSoundTime < bounceSoundCooldown) return;
        if (PlayRandomClip(bounceSFX, bounceVolume))
            lastBounceSoundTime = Time.time;
    }

    // Paddle hit sound. Falls back to the wall sound if no paddle clips are assigned.
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
                float wait = spawnTime + Random.Range(-spawnTimeVariation, spawnTimeVariation);
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
            float sideways = Random.Range(-startSidewaysSpeed, startSidewaysSpeed);
            float down = -Random.Range(startDownSpeedRange.x, startDownSpeedRange.y);
            SetVelocity(rb, new Vector2(sideways, down));
        }
        else
        {
            Debug.LogError("BallJuggle: the Ball Prefab needs a Rigidbody2D (Body Type Dynamic) so it can fall.");
        }
    }

    private Vector3 PickSpawnPosition()
    {
        // Balls always spawn in a strip along the TOP of the Play Area.
        float halfBall = 25f;
        RectTransform ballRect = ballPrefab != null ? ballPrefab.transform as RectTransform : null;
        if (ballRect != null) halfBall = ballRect.rect.height * 0.5f;

        float topY = playArea.rect.yMax - spawnTopMargin - halfBall;
        float minY = topY - ballSpawnHeight;

        // Never let the strip reach down toward the paddle.
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

            // Ball position and size in the Play Area's own space.
            Vector3 local = playArea.InverseTransformPoint(rt.position);
            float scaleRatio = playArea.lossyScale.x != 0f ? rt.lossyScale.x / playArea.lossyScale.x : 1f;
            float halfW = rt.rect.width * 0.5f * scaleRatio;
            float halfH = rt.rect.height * 0.5f * scaleRatio;

            Vector2 velocity = GetVelocity(rb);
            bool changed = false;
            bool wallBounced = false;
            bool paddleHit = false;

            // Left and right walls.
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

            // Top wall: a ball moving up that reaches the top edge is sent back down.
            if (bounceOffTop && local.y + halfH > maxY && velocity.y > 0f)
            {
                local.y = maxY - halfH;
                velocity.y = -velocity.y * topBounciness;
                changed = wallBounced = true;
            }

            // Paddle: only bounce a ball that is falling and is above the paddle's middle.
            if (hasPaddle && velocity.y < 0f && local.y > paddleRect.center.y)
            {
                bool overlapX = local.x + halfW > paddleRect.xMin && local.x - halfW < paddleRect.xMax;
                bool overlapY = local.y - halfH < paddleRect.yMax && local.y + halfH > paddleRect.yMin;

                if (overlapX && overlapY)
                {
                    local.y = paddleRect.yMax + halfH; // sit on top of the paddle

                    velocity.y = Mathf.Max(-velocity.y * paddleBounciness, minPaddleBounceSpeed);

                    // Hitting off-center steers the ball that way.
                    float halfPaddle = Mathf.Max(1f, paddleRect.width * 0.5f);
                    float offset = Mathf.Clamp((local.x - paddleRect.center.x) / halfPaddle, -1f, 1f);
                    velocity.x += offset * paddleSteer;

                    changed = paddleHit = true;
                }
            }

            if (changed)
            {
                rt.position = playArea.TransformPoint(local);
                SetVelocity(rb, velocity);
            }

            // One sound per ball per frame. The paddle sound wins if both happened.
            if (paddleHit) PlayPaddleHitSound();
            else if (wallBounced) PlayBounceSound();

            // Floor: the ball has fallen completely out of the bottom.
            if (local.y + halfH < floorY)
            {
                FailState();
                return;
            }
        }
    }

    // A RectTransform's bounds, measured in the Play Area's own space.
    private Rect LocalRect(RectTransform rt)
    {
        rt.GetWorldCorners(corners);
        Vector3 min = playArea.InverseTransformPoint(corners[0]);
        Vector3 max = playArea.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // Works on both older Unity versions and Unity 6.
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

    // ---------- Player (touch/mouse drag-to-move matching GalagaBlast) ----------

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