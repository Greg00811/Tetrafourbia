using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Collects horizontal finger/mouse drags on a UI element so the finale can
/// turn the player's view left and right (the classic swivel-in-your-chair look).
///
/// Put this on the same object as the finale's full-screen RawImage. Buttons
/// that sit on top of that image keep their own touches, so pressing a door or
/// light button never spins the view.
/// </summary>
public class DragLookArea : MonoBehaviour, IDragHandler
{
    private float pendingDelta;

    public void OnDrag(PointerEventData eventData)
    {
        pendingDelta += eventData.delta.x;
    }

    /// <summary>Returns how far the finger moved sideways (in pixels) since the last call.</summary>
    public float ConsumeDelta()
    {
        float delta = pendingDelta;
        pendingDelta = 0f;
        return delta;
    }

    private void OnDisable()
    {
        pendingDelta = 0f;
    }
}
