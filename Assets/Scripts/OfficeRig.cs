using UnityEngine;

/// <summary>
/// Put this on the ROOT of your 3D office. It is the bridge between your model
/// and the LightsAndDoorsFinale script: you point it at the parts of the office
/// that matter, and the finale script operates them without knowing how your
/// model is built.
///
/// For each side (left and right) it needs:
///   - the DOOR object, placed in its OPEN position in the scene
///   - how far the door moves to close (a slide, e.g. down 2.5 units)
///   - a LIGHT object that gets switched on while the player holds the light
///     button (a Light, a glowing mesh, or both inside one empty object)
///   - 3 empty objects down the hallway for the creature to stand at:
///     far away, closer, and right at the door. Put each where you want the
///     creature's CENTRE to be (roughly waist height).
///
/// And one more thing overall: an empty object where the player's eyes are.
/// </summary>
public class OfficeRig : MonoBehaviour
{
    [Header("Player's eyes")]
    [Tooltip("An empty object where the player sits. The view camera is placed here, looking along this object's blue (Z) arrow.")]
    public Transform cameraPoint;

    [System.Serializable]
    public class SideParts
    {
        [Tooltip("The door object. Leave it in its OPEN position in the scene.")]
        public Transform door;

        [Tooltip("How far the door moves to close, in the door's parent's local space. (0, -2.5, 0) drops it down; use X or Z to slide it sideways.")]
        public Vector3 closeOffset = new Vector3(0f, -2.5f, 0f);

        [Tooltip("Switched on while the player holds this side's light button.")]
        public GameObject lightObject;

        [Tooltip("Three empty objects down the hallway, in order: far, near, at the door.")]
        public Transform[] enemyStagePoints = new Transform[3];

        [System.NonSerialized] public Vector3 openLocalPosition;
    }

    public SideParts left = new SideParts();
    public SideParts right = new SideParts();

    private void Awake()
    {
        // Remember where the doors start (their open position) so we can always return to it.
        Remember(left);
        Remember(right);
    }

    private static void Remember(SideParts side)
    {
        if (side.door != null) side.openLocalPosition = side.door.localPosition;
    }

    /// <summary>0 = door fully open, 1 = fully closed.</summary>
    public void SetDoor(SideParts side, float closedAmount)
    {
        if (side.door == null) return;
        side.door.localPosition = side.openLocalPosition + side.closeOffset * Mathf.Clamp01(closedAmount);
    }

    public void SetLight(SideParts side, bool on)
    {
        if (side.lightObject != null && side.lightObject.activeSelf != on)
            side.lightObject.SetActive(on);
    }
}
