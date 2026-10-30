using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Microgame: "Swat the Flies"
///
/// 4 flies buzz around the screen with erratic, fly-like movement.
/// Each fly plays a looping 3-frame flying animation and gets a random hue.
/// Hold and swipe (mouse or finger) across a fly to kill it.
/// A single death sprite is left where each fly dies.
///
/// Kill all flies before time runs out = WIN.
/// Time runs out with a fly still alive = LOSE.
/// </summary>
public class SwatTheFlies : MicrogameBase
{
    [Header("Flies")]
    [Tooltip("The 4 fly UI objects (each needs an Image). All must share the same parent, e.g. a full-screen panel.")]
    [SerializeField] private RectTransform[] flies;

    [Header("Fly Animation")]
    [Tooltip("The frames of the flying animation, in order (3 frames, but any number works).")]
    [SerializeField] private Sprite[] flyFrames = new Sprite[3];
    [SerializeField] private float flyFrameRate = 12f;

    [Header("Fly Color")]
    [Tooltip("Material using the UI/HueShift shader. Only the colored parts change; black and white stay the same.")]
    [SerializeField] private Material hueShiftMaterial;
    [Tooltip("Make the death image use the same hue as the fly that died.")]
    [SerializeField] private bool tintDeathSprite = true;

    [Header("Fly Movement")]
    [SerializeField] private float minSpeed = 150f;
    [SerializeField] private float maxSpeed = 450f;

    [Tooltip("How fast the fly can turn (degrees per second). Higher = more erratic.")]
    [SerializeField] private float turnRate = 400f;

    [Tooltip("How quickly the wandering direction changes. Higher = more twitchy.")]
    [SerializeField] private float turnFrequency = 2.5f;

    [Tooltip("Chance per second that a fly picks a new random speed (darting/hovering).")]
    [SerializeField] private float speedChangeChance = 1.5f;

    [Tooltip("Tiny high-frequency shake in pixels, gives the buzzing feel.")]
    [SerializeField] private float jitterAmount = 3f;

    [Tooltip("Extra rotation offset so the sprite faces its movement direction. " +
             "Use -90 if your fly sprite points UP, 0 if it points RIGHT.")]
    [SerializeField] private float spriteRotationOffset = -90f;

    [Header("Swipe")]
    [Tooltip("Distance in pixels between hit checks along a swipe. Smaller = more accurate on fast swipes.")]
    [SerializeField] private float swipeSampleStep = 12f;

    [Header("Death")]
    [Tooltip("The single image that appears where a fly dies.")]
    [SerializeField] private Sprite deathSprite;
    [Tooltip("Size of the death image relative to the fly's size.")]
    [SerializeField] private float deathScale = 1f;

    [Header("Game Rules")]
    [SerializeField] private float timeLimit = 5f;
    [Tooltip("Small pause after the last kill so the death image can be seen before reporting the win.")]
    [SerializeField] private float winDelay = 0.35f;

    // Per-fly runtime data
    private Image[] flyImages;
    private float[] hues;          // hue shift per fly (0..1)
    private float[] angles;        // heading in radians
    private float[] speeds;        // current speed
    private float[] targetSpeeds;  // speed we're easing toward
    private float[] noiseSeeds;    // unique perlin offset per fly
    private float[] animOffsets;   // so flies don't flap in sync
    private bool[] alive;

    private readonly List<GameObject> splats = new List<GameObject>();

    private int killedCount;
    private float timeRemaining;
    private bool gameActive;

    private bool wasHeld;
    private Vector2 lastPointerPos;

    private Canvas parentCanvas;
    private Camera canvasCamera;

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        if (flies == null || flies.Length == 0)
        {
            Debug.LogError("SwatTheFlies: No flies assigned!");
            return;
        }

        if (flyFrames == null || flyFrames.Length == 0)
        {
            Debug.LogError("SwatTheFlies: Assign the fly animation frames!");
            return;
        }

        if (deathSprite == null)
        {
            Debug.LogError("SwatTheFlies: Death Sprite is not assigned!");
            return;
        }

        if (hueShiftMaterial == null)
        {
            Debug.LogError("SwatTheFlies: Hue Shift Material is not assigned!");
            return;
        }

        StopAllCoroutines();
        ClearSplats();

        // Work out which camera the canvas uses (null for Screen Space - Overlay).
        parentCanvas = flies[0].GetComponentInParent<Canvas>();
        canvasCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = parentCanvas.worldCamera;
        }

        int count = flies.Length;
        flyImages = new Image[count];
        hues = new float[count];
        angles = new float[count];
        speeds = new float[count];
        targetSpeeds = new float[count];
        noiseSeeds = new float[count];
        animOffsets = new float[count];
        alive = new bool[count];

        killedCount = 0;
        timeRemaining = timeLimit;
        wasHeld = false;

        for (int i = 0; i < count; i++)
        {
            if (flies[i] == null)
                continue;

            alive[i] = true;
            flies[i].gameObject.SetActive(true);

            flyImages[i] = flies[i].GetComponent<Image>();

            // Give each fly a random hue. The shader reads the hue shift from the
            // Image color's red channel, so black and white pixels are untouched.
            hues[i] = Random.value;

            if (flyImages[i] != null)
            {
                flyImages[i].material = hueShiftMaterial;
                flyImages[i].color = new Color(hues[i], 0f, 0f, 1f);
            }

            angles[i] = Random.Range(0f, Mathf.PI * 2f);
            speeds[i] = Random.Range(minSpeed, maxSpeed);
            targetSpeeds[i] = speeds[i];
            noiseSeeds[i] = Random.Range(0f, 1000f);
            animOffsets[i] = Random.Range(0f, flyFrames.Length);

            flies[i].anchoredPosition = GetRandomPosition(flies[i]);
        }

        gameActive = true;
    }

    private void Update()
    {
        if (!gameActive)
            return;

        // ----- Movement + animation -----
        for (int i = 0; i < flies.Length; i++)
        {
            if (flies[i] == null || !alive[i])
                continue;

            MoveFly(i);
            AnimateFly(i);
        }

        // ----- Swipe input -----
        HandleSwipe();

        // ----- Timer / lose condition -----
        if (!gameActive)
            return; // a win may have just triggered

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            gameActive = false;
            ReportResult(false);
        }
    }

    // ---------------------------------------------------------------
    // FLY ANIMATION
    // ---------------------------------------------------------------

    private void AnimateFly(int i)
    {
        if (flyImages[i] == null)
            return;

        int frame = (int)(Time.time * flyFrameRate + animOffsets[i]) % flyFrames.Length;
        flyImages[i].sprite = flyFrames[frame];
    }

    // ---------------------------------------------------------------
    // FLY MOVEMENT
    // ---------------------------------------------------------------

    private void MoveFly(int i)
    {
        RectTransform fly = flies[i];
        RectTransform parentRect = fly.parent as RectTransform;

        if (parentRect == null)
            return;

        float dt = Time.deltaTime;

        // Smooth, wandering turns using Perlin noise (range -1..1).
        float noise = (Mathf.PerlinNoise(noiseSeeds[i], Time.time * turnFrequency) - 0.5f) * 2f;
        angles[i] += noise * turnRate * Mathf.Deg2Rad * dt;

        // Occasionally pick a new speed so flies dart, slow down and hover.
        if (Random.value < speedChangeChance * dt)
        {
            targetSpeeds[i] = Random.value < 0.2f
                ? minSpeed * 0.3f                       // near-hover
                : Random.Range(minSpeed, maxSpeed);     // normal buzzing
        }
        speeds[i] = Mathf.Lerp(speeds[i], targetSpeeds[i], 5f * dt);

        Vector2 direction = new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i]));
        Vector2 position = fly.anchoredPosition + direction * speeds[i] * dt;

        // Tiny buzz jitter.
        position += Random.insideUnitCircle * jitterAmount * dt * 60f;

        // ----- Stay inside the canvas bounds -----
        float halfWidth = parentRect.rect.width * 0.5f;
        float halfHeight = parentRect.rect.height * 0.5f;
        float flyHalfWidth = fly.rect.width * 0.5f;
        float flyHalfHeight = fly.rect.height * 0.5f;

        float minX = -halfWidth + flyHalfWidth;
        float maxX = halfWidth - flyHalfWidth;
        float minY = -halfHeight + flyHalfHeight;
        float maxY = halfHeight - flyHalfHeight;

        bool bounced = false;

        if (position.x <= minX) { position.x = minX; direction.x = Mathf.Abs(direction.x); bounced = true; }
        else if (position.x >= maxX) { position.x = maxX; direction.x = -Mathf.Abs(direction.x); bounced = true; }

        if (position.y <= minY) { position.y = minY; direction.y = Mathf.Abs(direction.y); bounced = true; }
        else if (position.y >= maxY) { position.y = maxY; direction.y = -Mathf.Abs(direction.y); bounced = true; }

        if (bounced)
        {
            // Add a little randomness so they don't bounce in perfect lines.
            angles[i] = Mathf.Atan2(direction.y, direction.x) + Random.Range(-0.4f, 0.4f);
        }

        fly.anchoredPosition = position;

        // Face the direction of travel.
        float zRotation = angles[i] * Mathf.Rad2Deg + spriteRotationOffset;
        fly.localRotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    private Vector2 GetRandomPosition(RectTransform fly)
    {
        RectTransform parentRect = fly.parent as RectTransform;

        if (parentRect == null)
            return Vector2.zero;

        float halfWidth = parentRect.rect.width * 0.5f - fly.rect.width * 0.5f;
        float halfHeight = parentRect.rect.height * 0.5f - fly.rect.height * 0.5f;

        return new Vector2(
            Random.Range(-halfWidth, halfWidth),
            Random.Range(-halfHeight, halfHeight)
        );
    }

    // ---------------------------------------------------------------
    // SWIPE / KILL DETECTION
    // ---------------------------------------------------------------

    private void HandleSwipe()
    {
        // Input.GetMouseButton(0) also fires for the first finger on mobile.
        bool held = Input.GetMouseButton(0);
        Vector2 currentPos = Input.mousePosition;

        if (held)
        {
            if (!wasHeld)
            {
                // Finger/mouse just went down: check the starting point.
                lastPointerPos = currentPos;
                CheckPointForHits(currentPos);
            }
            else if (currentPos != lastPointerPos)
            {
                CheckSegmentForHits(lastPointerPos, currentPos);
                lastPointerPos = currentPos;
            }
        }

        wasHeld = held;
    }

    /// <summary>
    /// Samples points along the swipe so fast swipes can't skip over a fly.
    /// </summary>
    private void CheckSegmentForHits(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1f, swipeSampleStep)));

        for (int s = 1; s <= steps; s++)
        {
            if (!gameActive)
                return;

            Vector2 point = Vector2.Lerp(from, to, s / (float)steps);
            CheckPointForHits(point);
        }
    }

    private void CheckPointForHits(Vector2 screenPoint)
    {
        for (int i = 0; i < flies.Length; i++)
        {
            if (flies[i] == null || !alive[i])
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(flies[i], screenPoint, canvasCamera))
            {
                KillFly(i);

                if (!gameActive)
                    return;
            }
        }
    }

    // ---------------------------------------------------------------
    // KILLING + DEATH IMAGE
    // ---------------------------------------------------------------

    private void KillFly(int i)
    {
        alive[i] = false;
        killedCount++;

        SpawnDeathImage(i);
        flies[i].gameObject.SetActive(false);

        // Win condition: every fly is dead.
        if (killedCount >= flies.Length)
        {
            gameActive = false;
            StartCoroutine(WinAfterDelay());
        }
    }

    private void SpawnDeathImage(int i)
    {
        RectTransform fly = flies[i];

        GameObject splatObject = new GameObject("FlyDeath", typeof(RectTransform), typeof(Image));
        splatObject.transform.SetParent(fly.parent, false);

        RectTransform splatRect = splatObject.GetComponent<RectTransform>();
        splatRect.anchorMin = fly.anchorMin;
        splatRect.anchorMax = fly.anchorMax;
        splatRect.pivot = new Vector2(0.5f, 0.5f);
        splatRect.anchoredPosition = fly.anchoredPosition;
        splatRect.sizeDelta = fly.rect.size * deathScale;
        splatRect.localRotation = fly.localRotation; // keep the way the fly was facing

        Image splatImage = splatObject.GetComponent<Image>();
        splatImage.sprite = deathSprite;
        splatImage.raycastTarget = false;
        splatImage.preserveAspect = true;

        // Same hue shift as the fly that died (or no shift if disabled).
        splatImage.material = hueShiftMaterial;
        splatImage.color = tintDeathSprite ? new Color(hues[i], 0f, 0f, 1f) : Color.white;

        splats.Add(splatObject);
    }

    private IEnumerator WinAfterDelay()
    {
        yield return new WaitForSeconds(winDelay);
        ReportResult(true);
    }

    private void ClearSplats()
    {
        foreach (GameObject splat in splats)
        {
            if (splat != null)
                Destroy(splat);
        }

        splats.Clear();
    }

    // ---------------------------------------------------------------

    public override void ForceStop()
    {
        gameActive = false;
        StopAllCoroutines();
    }
}