using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Finale microgame: a Five Nights at Freddy's-style night shift with ONLY
/// lights and doors, played in YOUR 3D office, built for touch.
///
/// The rules:
///   - Something creeps down the LEFT hallway and something down the RIGHT one,
///     one step at a time (far -> near -> at your door).
///   - HOLD a light button to light up that hallway and see what is there.
///   - TAP a door button to close/open that door. A creature that reaches your
///     door while it is CLOSED gets blocked and wanders off. If it reaches an
///     OPEN door, it gets you and you lose.
///   - DRAG on the view to look left and right around the office.
///   - Lights and closed doors drain POWER. At 0% the doors pop open and the
///     lights die, so ration it.
///   - Survive until the clock hits 4 AM (the end of this microgame's timer) to win.
///
/// How it is shown: your 3D office is filmed by a camera that renders into a
/// RenderTexture, displayed on a RawImage inside your UI. The creatures are 2D
/// sprites standing in the 3D hallways, always turned to face the camera.
///
/// Your office needs an OfficeRig component on its root (see OfficeRig.cs) that
/// points at its doors, lights, creature spots and eye position.
///
/// Setup (UI):
///   1. Create a child object under MicroGameManager (like your other microgames)
///      with its own Canvas, and put this script on its root.
///   2. In that Canvas add, back to front: a full-screen RawImage (with a
///      DragLookArea on it), 2 Buttons (doors), 2 objects with an Image +
///      HoldButton (lights), a filled Image (power bar), and TMP texts for
///      power, clock and status.
///   3. Drag them into the fields below, disable the root object, and put it in
///      MicroGameManager's "Finale Microgame" slot (NOT the normal list).
/// </summary>
public class LightsAndDoorsFinale : MicrogameBase
{
    [Header("UI (inside this microgame's own Canvas)")]
    [SerializeField] private RawImage officeView;
    [SerializeField] private DragLookArea lookArea;      // on the same object as Office View
    [SerializeField] private Button leftDoorButton;
    [SerializeField] private HoldButton leftLightButton;
    [SerializeField] private Button rightDoorButton;
    [SerializeField] private HoldButton rightLightButton;
    [SerializeField] private Image powerFill;            // Image Type must be "Filled" (needs a Source Image)
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private TMP_Text statusText;        // optional: "BLOCKED!", "POWER OUT!"

    [Header("Your 3D office")]
    [Tooltip("Your office with an OfficeRig on its root. Either an object in the scene (disabled, placed far from the main camera) or a prefab from the Project window.")]
    [SerializeField] private GameObject officeObject;
    [Tooltip("Only used when the office is a prefab: where its copy is placed. Keep it far from the main camera.")]
    [SerializeField] private Vector3 worldOrigin = new Vector3(5000f, 0f, 0f);

    [Header("Creatures (2D sprites standing in the 3D hallways)")]
    [Tooltip("Leave empty to use a plain coloured block until you have art.")]
    [SerializeField] private Sprite leftEnemySprite;
    [SerializeField] private Sprite rightEnemySprite;
    [Tooltip("How tall a creature is in your office's units. Match this to the size of your model.")]
    [SerializeField] private float enemyHeight = 2f;
    [SerializeField] private float enemyWidth = 1f;      // only used for the plain placeholder block

    [Header("Camera")]
    [SerializeField] private Vector2Int viewResolution = new Vector2Int(720, 1280);
    [SerializeField] private float cameraFieldOfView = 70f;
    [SerializeField] private float lookSensitivity = 0.15f;   // degrees turned per pixel dragged
    [SerializeField] private float maxLookAngle = 40f;        // how far left/right you can look
    [SerializeField] private float swayAngle = 1.5f;
    [SerializeField] private float swayDistance = 0.04f;
    [SerializeField] private float swaySpeed = 0.6f;

    [Header("Lighting")]
    [Tooltip("Switches off the scene's directional light and dims ambient light while this plays, so only the office's own lights show. Everything is restored afterwards.")]
    [SerializeField] private bool darkenScene = true;
    [SerializeField] private Color ambientWhileActive = new Color(0.04f, 0.04f, 0.06f);

    [Header("Enemies")]
    [SerializeField] private float moveIntervalMin = 3.5f;
    [SerializeField] private float moveIntervalMax = 6.5f;
    [Tooltip("Enemies move this much faster by the end of the night (1 = no change, 0.6 = 40% faster).")]
    [SerializeField] private float finalAggression = 0.6f;

    [Header("Power")]
    [SerializeField] private float startingPower = 100f;
    [SerializeField] private float baseDrainPerSecond = 0.35f;
    [SerializeField] private float doorDrainPerSecond = 0.9f;   // per closed door
    [SerializeField] private float lightDrainPerSecond = 0.6f;  // per light that is on

    /// <summary>Everything that belongs to one side (left or right) of the office.</summary>
    private class Side
    {
        public OfficeRig.SideParts parts;
        public bool doorClosed;
        public bool lightOn;
        public int stage;            // 0 = gone, 1 = far, 2 = near, 3 = at the door
        public float moveTimer;
        public float doorAmount;     // 0 = open, 1 = closed (animated)
        public SpriteRenderer enemy;
    }

    private Side left;
    private Side right;
    private OfficeRig rig;
    private GameObject officeInstance;
    private bool officeIsCopy;
    private Transform lookPivot;
    private Camera officeCamera;
    private RenderTexture officeTexture;
    private Sprite whiteSprite;

    private float elapsed;
    private float power;
    private float statusTimer;
    private float lookYaw;
    private bool powerOut;
    private bool hasFailed;
    private bool roundOver;
    private bool setupFailed;

    // Saved so the scene's lighting can be put back exactly as it was.
    private readonly List<Light> dimmedLights = new List<Light>();
    private bool sceneDimmed;
    private UnityEngine.Rendering.AmbientMode savedAmbientMode;
    private Color savedAmbientColor;

    // Longer than a normal microgame, and surviving to the end of the timer is the win.
    public override bool IgnoreDifficultyRamp => true;
    public override bool TimeoutCountsAsWin => !hasFailed;

    // Runs when the script is first added in the editor, so the Inspector starts with sensible values.
    private void Reset()
    {
        instruction = "SURVIVE TILL 4 AM!";
        baseTimeLimit = 60f;
    }

    // ---------- Lifecycle ----------

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        CleanupWorld(); // in case a previous attempt left anything behind

        elapsed = 0f;
        power = startingPower;
        statusTimer = 0f;
        lookYaw = 0f;
        powerOut = false;
        hasFailed = false;
        roundOver = false;
        setupFailed = false;

        BuildWorld();
        if (!setupFailed) WireButtons();

        if (statusText != null) statusText.text = "";
        UpdateButtonTints();
        UpdateHud(0f);
    }

    public override void ForceStop()
    {
        roundOver = true; // stop updating; the world is cleaned up in OnDisable
    }

    private void OnDisable()
    {
        CleanupWorld();
    }

    private void OnDestroy()
    {
        CleanupWorld();
        if (whiteSprite != null) Destroy(whiteSprite);
    }

    // ---------- Per-frame update (driven by MicrogameManager) ----------

    public override void TickMicrogame(float deltaTime)
    {
        if (setupFailed)
        {
            // Setup is broken (see the Console). End the round instead of leaving the player stuck.
            if (!roundOver)
            {
                roundOver = true;
                hasFailed = true;
                ReportResult(false);
            }
            return;
        }

        if (roundOver || left == null || right == null) return;

        elapsed += deltaTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, BaseTimeLimit));

        // Lights follow the held buttons (and die when the power does).
        left.lightOn = !powerOut && leftLightButton != null && leftLightButton.IsHeld;
        right.lightOn = !powerOut && rightLightButton != null && rightLightButton.IsHeld;
        if (leftLightButton != null) leftLightButton.SetLit(left.lightOn);
        if (rightLightButton != null) rightLightButton.SetLit(right.lightOn);

        UpdatePower(deltaTime);

        float aggression = Mathf.Lerp(1f, finalAggression, progress);
        UpdateEnemy(left, deltaTime, aggression);
        if (roundOver) return;
        UpdateEnemy(right, deltaTime, aggression);
        if (roundOver) return;

        if (statusTimer > 0f)
        {
            statusTimer -= deltaTime;
            if (statusTimer <= 0f && !powerOut && statusText != null) statusText.text = "";
        }

        AnimateWorld(deltaTime);
        UpdateHud(progress);
    }

    // ---------- Game rules ----------

    private void WireButtons()
    {
        WireDoor(leftDoorButton, () => ToggleDoor(left));
        WireDoor(rightDoorButton, () => ToggleDoor(right));
    }

    private void WireDoor(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.interactable = true;
    }

    private void ToggleDoor(Side side)
    {
        if (side == null || roundOver || powerOut) return;
        side.doorClosed = !side.doorClosed;
        UpdateButtonTints();
    }

    private void UpdatePower(float deltaTime)
    {
        if (powerOut) return;

        float drain = baseDrainPerSecond;
        if (left.doorClosed) drain += doorDrainPerSecond;
        if (right.doorClosed) drain += doorDrainPerSecond;
        if (left.lightOn) drain += lightDrainPerSecond;
        if (right.lightOn) drain += lightDrainPerSecond;

        power = Mathf.Max(0f, power - drain * deltaTime);
        if (power <= 0f) TriggerPowerOut();
    }

    private void TriggerPowerOut()
    {
        powerOut = true;
        left.doorClosed = false;
        right.doorClosed = false;
        left.lightOn = false;
        right.lightOn = false;

        if (leftDoorButton != null) leftDoorButton.interactable = false;
        if (rightDoorButton != null) rightDoorButton.interactable = false;

        ShowStatus("POWER OUT!", 0f); // stays up for the rest of the night
        UpdateButtonTints();
    }

    private void UpdateEnemy(Side side, float deltaTime, float aggression)
    {
        side.moveTimer -= deltaTime;
        if (side.moveTimer > 0f) return;
        side.moveTimer = Random.Range(moveIntervalMin, moveIntervalMax) * aggression;

        if (side.stage < 3)
        {
            side.stage++; // creeps one step closer
            return;
        }

        // It is at the door and it is time to act.
        if (side.doorClosed)
        {
            side.stage = 0; // blocked - it wanders off
            ShowStatus("BLOCKED!", 1f);
        }
        else
        {
            roundOver = true;
            hasFailed = true;
            StartCoroutine(JumpscareThenLose(side));
        }
    }

    private IEnumerator JumpscareThenLose(Side side)
    {
        ShowStatus("!!!", 0f);

        // Put the creature right in front of the camera, filling the screen.
        Transform cam = officeCamera.transform;
        const float distance = 1.2f;
        float visibleHeight = 2f * distance * Mathf.Tan(officeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        side.enemy.enabled = true;
        side.enemy.transform.position = cam.position + cam.forward * distance;
        side.enemy.transform.rotation = cam.rotation;
        side.enemy.transform.localScale *= (visibleHeight * 1.2f) / Mathf.Max(0.01f, enemyHeight);

        yield return new WaitForSeconds(0.5f);
        ReportResult(false);
    }

    private void ShowStatus(string message, float duration)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusTimer = duration; // 0 = leave it up
    }

    // ---------- Visuals ----------

    private void AnimateWorld(float deltaTime)
    {
        // Drag to look around, plus a slow sway so the room never feels frozen.
        if (lookArea != null)
        {
            lookYaw = Mathf.Clamp(lookYaw + lookArea.ConsumeDelta() * lookSensitivity, -maxLookAngle, maxLookAngle);
        }

        float t = Time.time * swaySpeed;
        lookPivot.localRotation = Quaternion.Euler(0f, lookYaw + Mathf.Sin(t) * swayAngle, 0f);
        officeCamera.transform.localPosition = new Vector3(
            Mathf.Sin(t) * swayDistance,
            Mathf.Sin(t * 0.7f + 1f) * swayDistance * 0.3f,
            0f);

        AnimateSide(left, deltaTime);
        AnimateSide(right, deltaTime);
    }

    private void AnimateSide(Side side, float deltaTime)
    {
        // Door slides shut/open, light follows the button.
        side.doorAmount = Mathf.MoveTowards(side.doorAmount, side.doorClosed ? 1f : 0f, deltaTime * 4f);
        rig.SetDoor(side.parts, side.doorAmount);
        rig.SetLight(side.parts, side.lightOn);

        // The creature is only visible while the light is on, standing at its current spot.
        Transform spot = StagePoint(side);
        bool visible = side.lightOn && side.stage > 0 && spot != null;
        side.enemy.enabled = visible;

        if (spot != null)
        {
            side.enemy.transform.position = spot.position;
        }
        // Turn it to face the camera so the flat sprite always reads clearly.
        side.enemy.transform.rotation = officeCamera.transform.rotation;
    }

    private static Transform StagePoint(Side side)
    {
        Transform[] points = side.parts.enemyStagePoints;
        if (side.stage <= 0 || points == null || points.Length == 0) return null;
        return points[Mathf.Clamp(side.stage - 1, 0, points.Length - 1)];
    }

    private void UpdateHud(float progress)
    {
        float percent = power / Mathf.Max(1f, startingPower);

        if (powerFill != null) powerFill.fillAmount = percent;
        if (powerText != null) powerText.text = $"POWER {Mathf.CeilToInt(percent * 100f)}%";

        if (clockText != null)
        {
            int hour = Mathf.Min(4, Mathf.FloorToInt(progress * 4f)); // 12, 1, 2, 3, 4 AM
            clockText.text = hour == 0 ? "12 AM" : $"{hour} AM";
        }
    }

    private void UpdateButtonTints()
    {
        TintDoorButton(leftDoorButton, left != null && left.doorClosed);
        TintDoorButton(rightDoorButton, right != null && right.doorClosed);
    }

    private static void TintDoorButton(Button button, bool closed)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = closed ? new Color(1f, 0.45f, 0.45f) : Color.white;
    }

    // ---------- Setting up and tearing down the office view ----------

    private void BuildWorld()
    {
        if (officeObject == null)
        {
            Fail("No office assigned. Drag your office into the 'Office Object' field.");
            return;
        }

        // A scene object is used as it is; a prefab asset gets a copy made far away.
        if (officeObject.scene.IsValid())
        {
            officeInstance = officeObject;
            officeIsCopy = false;
        }
        else
        {
            officeInstance = Instantiate(officeObject, worldOrigin, Quaternion.identity);
            officeIsCopy = true;
        }
        officeInstance.SetActive(true);

        rig = officeInstance.GetComponentInChildren<OfficeRig>(true);
        if (rig == null)
        {
            Fail("The office has no OfficeRig component. Add one to the office's root object.");
            return;
        }
        if (rig.cameraPoint == null)
        {
            Fail("The OfficeRig has no Camera Point. Add an empty object where the player sits and assign it.");
            return;
        }

        // Start from a clean state: doors open, lights off.
        rig.SetDoor(rig.left, 0f);
        rig.SetDoor(rig.right, 0f);
        rig.SetLight(rig.left, false);
        rig.SetLight(rig.right, false);

        // The camera sits on a pivot at the player's eyes, so the pivot can turn to look around.
        GameObject pivotObject = new GameObject("LookPivot");
        lookPivot = pivotObject.transform;
        lookPivot.SetParent(rig.cameraPoint, false);

        GameObject cameraObject = new GameObject("OfficeCamera");
        cameraObject.transform.SetParent(lookPivot, false);
        officeCamera = cameraObject.AddComponent<Camera>();
        officeCamera.orthographic = false;
        officeCamera.fieldOfView = cameraFieldOfView;
        officeCamera.nearClipPlane = 0.05f;
        officeCamera.farClipPlane = 200f;
        officeCamera.clearFlags = CameraClearFlags.SolidColor;
        officeCamera.backgroundColor = Color.black;
        officeCamera.depth = -10f;

        // Film into a texture instead of the screen, and show that texture in the UI.
        officeTexture = new RenderTexture(viewResolution.x, viewResolution.y, 24);
        officeCamera.targetTexture = officeTexture;
        if (officeView != null) officeView.texture = officeTexture;

        if (whiteSprite == null) whiteSprite = CreateWhiteSprite();
        left = BuildSide(rig.left, "Left", leftEnemySprite, new Color(0.9f, 0.15f, 0.15f), 3f, 5f);
        right = BuildSide(rig.right, "Right", rightEnemySprite, new Color(0.95f, 0.55f, 0.10f), 5f, 8f);

        DarkenScene();
    }

    private void Fail(string message)
    {
        Debug.LogError("LightsAndDoorsFinale: " + message);
        setupFailed = true;
    }

    private Side BuildSide(OfficeRig.SideParts parts, string label, Sprite sprite,
        Color placeholderColor, float minDelay, float maxDelay)
    {
        Side side = new Side();
        side.parts = parts;

        // Creatures live at the scene root (not inside the office) so the office's own scale can't distort them.
        GameObject enemyObject = new GameObject(label + "Enemy");
        side.enemy = enemyObject.AddComponent<SpriteRenderer>();

        bool usingArt = sprite != null;
        side.enemy.sprite = usingArt ? sprite : whiteSprite;
        side.enemy.color = usingArt ? Color.white : placeholderColor;

        Vector2 native = side.enemy.sprite.bounds.size;
        float scaleY = enemyHeight / native.y;
        float scaleX = usingArt ? scaleY : enemyWidth / native.x; // art keeps its own proportions
        enemyObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        side.enemy.enabled = false;

        // Stagger the two sides so they don't both arrive at once.
        side.moveTimer = Random.Range(minDelay, maxDelay);
        return side;
    }

    // A 1x1-unit white square, so placeholder creatures need no art assets at all.
    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
    }

    private void DarkenScene()
    {
        if (!darkenScene || sceneDimmed) return;

        // Switch off other directional lights (a new scene comes with one) so
        // the office's own lights are what you see.
        dimmedLights.Clear();
        foreach (Light sceneLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            bool isOfficeLight = officeInstance != null && sceneLight.transform.IsChildOf(officeInstance.transform);
            if (sceneLight.type == LightType.Directional && sceneLight.enabled && !isOfficeLight)
            {
                sceneLight.enabled = false;
                dimmedLights.Add(sceneLight);
            }
        }

        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbientColor = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientWhileActive;
        sceneDimmed = true;
    }

    private void RestoreScene()
    {
        if (!sceneDimmed) return;

        foreach (Light sceneLight in dimmedLights)
        {
            if (sceneLight != null) sceneLight.enabled = true;
        }
        dimmedLights.Clear();

        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientColor;
        sceneDimmed = false;
    }

    private void CleanupWorld()
    {
        if (officeCamera != null)
        {
            officeCamera.enabled = false; // stop it before its texture goes away
            officeCamera.targetTexture = null;
        }
        if (officeView != null) officeView.texture = null;

        if (officeTexture != null)
        {
            officeTexture.Release();
            Destroy(officeTexture);
            officeTexture = null;
        }

        if (lookPivot != null)
        {
            Destroy(lookPivot.gameObject); // takes the camera with it
            lookPivot = null;
        }

        DestroyEnemy(left);
        DestroyEnemy(right);

        if (officeInstance != null)
        {
            if (officeIsCopy) Destroy(officeInstance);
            else officeInstance.SetActive(false);
        }

        RestoreScene();

        officeCamera = null;
        officeInstance = null;
        rig = null;
        left = null;
        right = null;
    }

    private static void DestroyEnemy(Side side)
    {
        if (side != null && side.enemy != null) Destroy(side.enemy.gameObject);
    }
}
