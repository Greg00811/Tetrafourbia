using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Finale microgame: a night shift with ONLY lights and doors, in plain 2D.
/// You only assign sprites. The script creates and positions all the pictures itself.
///
///   - HOLD a light button to light up that hallway and see what is there.
///   - TAP a door button to close/open that door.
///   - A creature at a CLOSED door is blocked. At an OPEN door, it gets you.
///   - Lights and closed doors drain POWER. At 0% the doors open and lights die.
///   - Survive until the timer ends (4 AM) to win.
///
/// Positions are in "screen fractions": (0,0) is the bottom-left of the screen,
/// (1,1) is the top-right, (0.5,0.5) is the middle. Heights are a fraction of
/// the screen height (0.5 = half the screen tall).
/// </summary>
public class LightsAndDoorsFinale : MicrogameBase
{
    /// <summary>The pictures and placement for one side of the office.</summary>
    [System.Serializable]
    public class SideArt
    {
        [Header("Light")]
        [Tooltip("Full-screen picture shown while this side's light button is held (a glow, or the office with the light on).")]
        public Sprite lightSprite;

        [Header("Door")]
        [Tooltip("Picture of the closed door.")]
        public Sprite doorSprite;
        public Vector2 doorPosition = new Vector2(0.2f, 0.45f);
        [Range(0.05f, 1.5f)] public float doorHeight = 0.6f;

        [Header("Creature")]
        [Tooltip("Leave empty to use a plain coloured block.")]
        public Sprite enemySprite;
        public Color placeholderColor = Color.red;
        [Tooltip("Where it stands when it is far away (stage 1).")]
        public Vector2 enemyFarPosition = new Vector2(0.12f, 0.5f);
        [Tooltip("Where it stands when it is at your door (stage 3). Stage 2 is halfway between.")]
        public Vector2 enemyDoorPosition = new Vector2(0.25f, 0.45f);
        [Range(0.05f, 1.5f)] public float enemyFarHeight = 0.2f;
        [Range(0.05f, 1.5f)] public float enemyDoorHeight = 0.5f;
        [Tooltip("Flip the picture sideways (handy to reuse one creature picture on both sides).")]
        public bool flipHorizontally = false;
    }

    [Header("UI (inside this microgame's own Canvas)")]
    [SerializeField] private Button leftDoorButton;
    [SerializeField] private HoldButton leftLightButton;
    [SerializeField] private Button rightDoorButton;
    [SerializeField] private HoldButton rightLightButton;
    [SerializeField] private Image powerFill;            // Image Type must be "Filled" (needs a Source Image)
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private TMP_Text statusText;        // optional: "BLOCKED!", "POWER OUT!"

    [Header("Office pictures (just drag sprites in)")]
    [Tooltip("The office picture. Drawn full screen behind everything.")]
    [SerializeField] private Sprite officeSprite;
    [SerializeField]
    private SideArt leftSide = new SideArt
    {
        doorPosition = new Vector2(0.2f, 0.45f),
        enemyFarPosition = new Vector2(0.12f, 0.5f),
        enemyDoorPosition = new Vector2(0.25f, 0.45f),
        placeholderColor = new Color(0.9f, 0.15f, 0.15f)
    };
    [SerializeField]
    private SideArt rightSide = new SideArt
    {
        doorPosition = new Vector2(0.8f, 0.45f),
        enemyFarPosition = new Vector2(0.88f, 0.5f),
        enemyDoorPosition = new Vector2(0.75f, 0.45f),
        placeholderColor = new Color(0.95f, 0.55f, 0.1f),
        flipHorizontally = true
    };

    [Header("Jumpscare")]
    [SerializeField] private Vector2 jumpscarePosition = new Vector2(0.5f, 0.5f);
    [Tooltip("Height of the creature when it gets you, as a fraction of the screen height.")]
    [SerializeField] private float jumpscareHeight = 1.3f;

    [Header("Hide UI")]
    [Tooltip("Objects to hide: buttons, power bar, texts, etc. Do NOT put this script's own root object here.")]
    [SerializeField] private GameObject[] hideableUI;
    [SerializeField] private bool hideUIDuringJumpscare = true;
    [SerializeField] private bool hideUIWhenPowerOut = false;
    [Tooltip("Optional: a button that toggles the UI on and off. Don't put it in the list above, or it will hide itself.")]
    [SerializeField] private Button toggleUIButton;

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

    private class Side
    {
        public SideArt art;
        public Image lightImage;
        public Image doorImage;
        public Image enemyImage;
        public bool doorClosed;
        public bool lightOn;
        public int stage;            // 0 = gone, 1 = far, 2 = near, 3 = at the door
        public float moveTimer;
    }

    private Side left;
    private Side right;
    private RectTransform artRoot;     // holds all the pictures, behind the buttons
    private RectTransform frontRoot;   // created for the jumpscare, on top of everything

    private float elapsed;
    private float power;
    private float statusTimer;
    private bool powerOut;
    private bool hasFailed;
    private bool roundOver;
    private bool uiHidden;

    // Longer than a normal microgame, and surviving to the end of the timer is the win.
    public override bool IgnoreDifficultyRamp => true;
    public override bool TimeoutCountsAsWin => !hasFailed;

    private void Reset()
    {
        instruction = "SURVIVE TILL 4 AM!";
        baseTimeLimit = 60f;
    }

    // ---------- Lifecycle ----------

    public override void StartMicrogame()
    {
        base.StartMicrogame();

        CleanupArt(); // in case a previous attempt left anything behind

        elapsed = 0f;
        power = startingPower;
        statusTimer = 0f;
        powerOut = false;
        hasFailed = false;
        roundOver = false;

        BuildArt();

        SetUIHidden(false); // always start with the UI showing

        if (toggleUIButton != null)
        {
            toggleUIButton.onClick.RemoveAllListeners();
            toggleUIButton.onClick.AddListener(ToggleUI);
        }

        WireDoor(leftDoorButton, () => ToggleDoor(left));
        WireDoor(rightDoorButton, () => ToggleDoor(right));

        if (statusText != null) statusText.text = "";
        ApplyVisuals();
        UpdateButtonTints();
        UpdateHud(0f);
    }

    public override void ForceStop()
    {
        roundOver = true;
    }

    private void OnDisable()
    {
        SetUIHidden(false); // never leave the UI hidden
        CleanupArt();
    }

    // ---------- Per-frame update (driven by MicrogameManager) ----------

    public override void TickMicrogame(float deltaTime)
    {
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

        ApplyVisuals();
        UpdateHud(progress);
    }

    // ---------- Game rules ----------

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
        ApplyVisuals();
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
        if (hideUIWhenPowerOut) SetUIHidden(true);

        left.doorClosed = false;
        right.doorClosed = false;
        left.lightOn = false;
        right.lightOn = false;

        if (leftDoorButton != null) leftDoorButton.interactable = false;
        if (rightDoorButton != null) rightDoorButton.interactable = false;

        ShowStatus("POWER OUT!", 0f);
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
        if (hideUIDuringJumpscare) SetUIHidden(true);

        if (side.enemyImage != null)
        {
            // Move the creature to a layer above the buttons and fill the screen with it.
            if (frontRoot == null) frontRoot = CreateFullScreenRoot("FinaleFront");
            frontRoot.SetAsLastSibling();
            side.enemyImage.rectTransform.SetParent(frontRoot, false);
            side.enemyImage.enabled = true;
            PlacePicture(side.enemyImage, side.art.enemySprite, jumpscarePosition, jumpscareHeight, side.art.flipHorizontally);
        }

        yield return new WaitForSeconds(0.5f);
        ReportResult(false);
    }

    private void ShowStatus(string message, float duration)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusTimer = duration; // 0 = leave it up
    }

    // ---------- Hide UI ----------

    // Hides or shows everything in the Hideable UI list.
    public void SetUIHidden(bool hidden)
    {
        uiHidden = hidden;
        if (hideableUI == null) return;

        foreach (GameObject obj in hideableUI)
            if (obj != null) obj.SetActive(!hidden);
    }

    // Handy to hook up to a button's OnClick.
    public void ToggleUI()
    {
        SetUIHidden(!uiHidden);
    }

    // ---------- Visuals ----------

    private void ApplyVisuals()
    {
        ApplySide(left);
        ApplySide(right);
    }

    private void ApplySide(Side side)
    {
        if (side == null) return;

        if (side.lightImage != null) side.lightImage.enabled = side.lightOn;
        if (side.doorImage != null) side.doorImage.enabled = side.doorClosed;

        if (side.enemyImage == null) return;

        // The creature is only visible while the light is on and it is somewhere in the hallway.
        bool visible = side.lightOn && side.stage > 0;
        side.enemyImage.enabled = visible;
        if (!visible) return;

        // Stage 1 = far, stage 3 = at the door, stage 2 = halfway.
        float t = (side.stage - 1) / 2f;
        Vector2 position = Vector2.Lerp(side.art.enemyFarPosition, side.art.enemyDoorPosition, t);
        float height = Mathf.Lerp(side.art.enemyFarHeight, side.art.enemyDoorHeight, t);
        PlacePicture(side.enemyImage, side.art.enemySprite, position, height, side.art.flipHorizontally);
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

    // ---------- Building the pictures in code ----------

    private void BuildArt()
    {
        artRoot = CreateFullScreenRoot("FinaleArt");
        artRoot.SetAsFirstSibling(); // behind your buttons and HUD

        // Back to front: office, lights, doors, creatures.
        if (officeSprite != null)
        {
            Image office = CreateImage("Office", artRoot, officeSprite);
            Stretch(office.rectTransform);
        }

        left = new Side { art = leftSide, moveTimer = Random.Range(3f, 5f) };
        right = new Side { art = rightSide, moveTimer = Random.Range(5f, 8f) };

        left.lightImage = CreateLightImage("LeftLight", leftSide);
        right.lightImage = CreateLightImage("RightLight", rightSide);

        left.doorImage = CreateDoorImage("LeftDoor", leftSide);
        right.doorImage = CreateDoorImage("RightDoor", rightSide);

        left.enemyImage = CreateEnemyImage("LeftEnemy", leftSide);
        right.enemyImage = CreateEnemyImage("RightEnemy", rightSide);
    }

    private Image CreateLightImage(string name, SideArt art)
    {
        if (art.lightSprite == null) return null;
        Image image = CreateImage(name, artRoot, art.lightSprite);
        Stretch(image.rectTransform);
        image.enabled = false;
        return image;
    }

    private Image CreateDoorImage(string name, SideArt art)
    {
        if (art.doorSprite == null) return null;
        Image image = CreateImage(name, artRoot, art.doorSprite);
        PlacePicture(image, art.doorSprite, art.doorPosition, art.doorHeight, false);
        image.enabled = false;
        return image;
    }

    private Image CreateEnemyImage(string name, SideArt art)
    {
        Image image = CreateImage(name, artRoot, art.enemySprite);
        if (art.enemySprite == null) image.color = art.placeholderColor; // plain block until you have art
        PlacePicture(image, art.enemySprite, art.enemyFarPosition, art.enemyFarHeight, art.flipHorizontally);
        image.enabled = false;
        return image;
    }

    private RectTransform CreateFullScreenRoot(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        Stretch(rt);
        return rt;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false; // never block your buttons
        return image;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Puts a picture at a screen-fraction position with a given height (fraction of screen height).
    private void PlacePicture(Image image, Sprite sprite, Vector2 position, float heightFraction, bool flip)
    {
        RectTransform rt = image.rectTransform;
        RectTransform container = rt.parent as RectTransform;
        float screenHeight = container != null ? container.rect.height : Screen.height;

        float aspect = 0.5f; // plain placeholder block is a tall rectangle
        if (sprite != null && sprite.rect.height > 0f) aspect = sprite.rect.width / sprite.rect.height;

        float height = screenHeight * heightFraction;

        rt.anchorMin = position;
        rt.anchorMax = position;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(height * aspect, height);
        rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
    }

    private void CleanupArt()
    {
        if (artRoot != null) Destroy(artRoot.gameObject);
        if (frontRoot != null) Destroy(frontRoot.gameObject);
        artRoot = null;
        frontRoot = null;
        left = null;
        right = null;
    }
}