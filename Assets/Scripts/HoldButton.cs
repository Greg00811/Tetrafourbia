using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A UI element that reports whether a finger (or mouse button) is currently
/// held down on it. A regular Button only tells you about clicks, but
/// "hold to shine the light" needs press AND release.
///
/// Put this on a UI object that has an Image with Raycast Target ticked
/// (that's what makes it receive touches). It works with touch and mouse.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Color heldColor = new Color(1f, 0.95f, 0.5f);

    public bool IsHeld { get; private set; }

    private Graphic graphic;

    public void OnPointerDown(PointerEventData eventData) { IsHeld = true; }
    public void OnPointerUp(PointerEventData eventData) { IsHeld = false; }

    // Sliding the finger off the button counts as letting go.
    public void OnPointerExit(PointerEventData eventData) { IsHeld = false; }

    private void OnDisable()
    {
        IsHeld = false;
        SetLit(false);
    }

    /// <summary>Tints the button while its light is on, so the player can see it's active.</summary>
    public void SetLit(bool lit)
    {
        if (graphic == null) graphic = GetComponent<Graphic>();
        if (graphic != null) graphic.color = lit ? heldColor : Color.white;
    }
}
