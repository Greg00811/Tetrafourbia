using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Example "intensive" microgame: a compact Galaga-style shooter, built for
/// touch/mobile play. A grid of enemies marches side to side and steps
/// downward; the player drags left/right to move a ship that auto-fires the
/// whole time a finger (or mouse, for testing in-editor) is down. Clear every
/// enemy before the timer runs out to win; if an enemy reaches the player's
/// row, it's a loss.
///
/// Controls: touch-and-drag anywhere on the PlayArea to move the ship
/// horizontally toward your finger; the ship fires automatically on a
/// cooldown for as long as you're touching. No on-screen buttons needed,
/// which keeps this thumb-friendly on a phone screen.
///
/// This follows the exact same MicrogameBase pattern as TapFourTimes - it
/// just has more moving parts. Everything stays inside plain UI RectTransforms
/// (no Physics2D, no Rigidbody) so it fits naturally into the same Canvas-based
/// microgame system as your other games, and TickMicrogame() is driven by
/// MicrogameManager every frame rather than Unity's own Update().
///
/// Setup:
///   1. Create a child GameObject under your microgame container (like
///      TapFourTimes), sized to fill the play area. This is the root object
///      you'll drag into MicrogameManager's "Available Microgames" list.
///   2. Inside it, add a RectTransform "PlayArea" that defines the bounds
///      enemies/bullets/player can move within - give it an Image component
///      (even fully transparent) so it can receive touch input as a raycast
///      target; this is what the player will actually drag their finger on.
///   3. Add a small UI Image as the player ship near the bottom of PlayArea.
///   4. Make two simple prefabs (Assets, not scene objects): a small UI Image
///      for "EnemyPrefab" and a smaller/thinner UI Image for "BulletPrefab".
///      Each just needs a RectTransform + Image component.
///   5. Add two empty RectTransforms as containers: "EnemyContainer" and
///      "BulletContainer" - spawned enemies/bullets get parented here so
///      they're easy to clear between rounds.
///   6. Attach this script to the root object and wire up all the fields,
///      including dragging PlayArea's own Canvas into "Parent Canvas" below.
///   7. Set this root object inactive in the Hierarchy (the manager activates
///      it on its turn) and drag it into MicrogameManager's array.
/// </summary>
public class GalagaBlast : MicrogameBase
{
    [Header("Play Area & Player")]
    [SerializeField] private RectTransform playArea;   // defines movement bounds AND receives touch input
    [SerializeField] private Canvas parentCanvas;       // needed to convert screen touch position correctly
    [SerializeField] private RectTransform playerShip;
    [SerializeField] private float playerFollowSpeed = 900f; // how quickly the ship catches up to your finger
    [SerializeField] private float playerHalfWidth = 30f;

    [Header("Firing (auto-fires while touching)")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletContainer;
    [SerializeField] private float bulletSpeed = 500f;
    [SerializeField] private float fireCooldown = 0.3f;

    [Header("Enemy Formation")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyContainer;
    [SerializeField] private int rows = 2;
    [SerializeField] private int columns = 4; // 4 columns, on theme
    [SerializeField] private float enemySpacingX = 90f;
    [SerializeField] private float enemySpacingY = 60f;
    [SerializeField] private float enemySidewaysSpeed = 60f;
    [SerializeField] private float enemyDropAmount = 40f;
    [SerializeField] private float enemyHalfSize = 25f;
    [SerializeField] private float loseRowY = -260f; // anchoredPosition.y that counts as "reached the player"

    private readonly List<RectTransform> activeEnemies = new List<RectTransform>();
    private readonly List<RectTransform> activeBullets = new List<RectTransform>();
    private float enemyDirection = 1f; // 1 = moving right, -1 = moving left
    private float fireTimer;
    private bool roundOver;

    public override void StartMicrogame()
    {
        base.StartMicrogame();
        roundOver = false;
        fireTimer = 0f;
        enemyDirection = 1f;

        ClearContainer(enemyContainer, activeEnemies);
        ClearContainer(bulletContainer, activeBullets);

        SpawnFormation();

        if (playerShip != null)
        {
            playerShip.anchoredPosition = new Vector2(0f, playerShip.anchoredPosition.y);
        }
    }

    public override void TickMicrogame(float deltaTime)
    {
        if (roundOver) return;

        HandlePlayerMovement(deltaTime);
        HandleFiring(deltaTime);
        MoveEnemyFormation(deltaTime);
        MoveBullets(deltaTime);
        CheckCollisions();
        CheckLoseCondition();
    }

    public override void ForceStop()
    {
        roundOver = true;
        ClearContainer(enemyContainer, activeEnemies);
        ClearContainer(bulletContainer, activeBullets);
    }

    // ---------- Setup ----------

    private void SpawnFormation()
    {
        if (enemyPrefab == null || enemyContainer == null) return;

        float startX = -((columns - 1) * enemySpacingX) / 2f;
        float startY = playArea != null ? playArea.rect.height * 0.35f : 150f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                GameObject enemyObj = Instantiate(enemyPrefab, enemyContainer);
                RectTransform rt = enemyObj.GetComponent<RectTransform>();
                if (rt == null) continue;

                rt.anchoredPosition = new Vector2(startX + col * enemySpacingX, startY - row * enemySpacingY);
                enemyObj.SetActive(true);
                activeEnemies.Add(rt);
            }
        }
    }

    private void ClearContainer(Transform container, List<RectTransform> list)
    {
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
        list.Clear();
    }

    // ---------- Player (touch/mouse drag-to-move, auto-fire while held) ----------

    /// <summary>
    /// True while the player currently has a finger (or mouse button, for
    /// editor testing) down anywhere - used to drive both movement and
    /// auto-fire from the same touch.
    /// </summary>
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

    private void HandleFiring(float deltaTime)
    {
        fireTimer -= deltaTime;

        if (IsPointerDown(out _) && fireTimer <= 0f)
        {
            fireTimer = fireCooldown;
            FireBullet();
        }
    }

    private void FireBullet()
    {
        if (bulletPrefab == null || bulletContainer == null || playerShip == null) return;

        GameObject bulletObj = Instantiate(bulletPrefab, bulletContainer);
        RectTransform rt = bulletObj.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchoredPosition = playerShip.anchoredPosition + new Vector2(0f, 20f);
        bulletObj.SetActive(true);
        activeBullets.Add(rt);
    }

    // ---------- Enemies & Bullets ----------

    private void MoveEnemyFormation(float deltaTime)
    {
        if (activeEnemies.Count == 0 || playArea == null) return;

        float halfWidth = playArea.rect.width / 2f - enemyHalfSize;
        bool hitEdge = false;

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;
            Vector2 pos = enemy.anchoredPosition;
            pos.x += enemyDirection * enemySidewaysSpeed * deltaTime;
            enemy.anchoredPosition = pos;

            if (pos.x >= halfWidth || pos.x <= -halfWidth)
                hitEdge = true;
        }

        if (hitEdge)
        {
            enemyDirection *= -1f;
            foreach (var enemy in activeEnemies)
            {
                if (enemy == null) continue;
                Vector2 pos = enemy.anchoredPosition;
                pos.y -= enemyDropAmount;
                enemy.anchoredPosition = pos;
            }
        }
    }

    private void MoveBullets(float deltaTime)
    {
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            RectTransform bullet = activeBullets[i];
            if (bullet == null)
            {
                activeBullets.RemoveAt(i);
                continue;
            }

            Vector2 pos = bullet.anchoredPosition;
            pos.y += bulletSpeed * deltaTime;
            bullet.anchoredPosition = pos;

            float topEdge = playArea != null ? playArea.rect.height / 2f : 400f;
            if (pos.y > topEdge)
            {
                Destroy(bullet.gameObject);
                activeBullets.RemoveAt(i);
            }
        }
    }

    private void CheckCollisions()
    {
        const float hitRadius = 30f;

        for (int b = activeBullets.Count - 1; b >= 0; b--)
        {
            RectTransform bullet = activeBullets[b];
            if (bullet == null) continue;

            for (int e = activeEnemies.Count - 1; e >= 0; e--)
            {
                RectTransform enemy = activeEnemies[e];
                if (enemy == null) continue;

                if (Vector2.Distance(bullet.anchoredPosition, enemy.anchoredPosition) < hitRadius)
                {
                    Destroy(bullet.gameObject);
                    Destroy(enemy.gameObject);
                    activeBullets.RemoveAt(b);
                    activeEnemies.RemoveAt(e);
                    break;
                }
            }

            if (bullet == null) continue; // was destroyed above, move to next bullet
        }

        if (activeEnemies.Count == 0 && !roundOver)
        {
            roundOver = true;
            ReportResult(true); // formation cleared - win
        }
    }

    private void CheckLoseCondition()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;
            if (enemy.anchoredPosition.y <= loseRowY)
            {
                roundOver = true;
                ReportResult(false); // an enemy reached the player's row - lose
                return;
            }
        }
    }
}
