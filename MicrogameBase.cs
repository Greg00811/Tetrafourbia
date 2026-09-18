using System;
using UnityEngine;

/// <summary>
/// Convenience base class for microgames. Handles the IMicrogame event
/// plumbing so individual microgame scripts just call ReportResult(true/false)
/// and override the three lifecycle methods they actually need.
///
/// Each microgame lives on its own GameObject/prefab with its own UI,
/// and is registered in the MicrogameManager's "Available Microgames" list.
/// Only one microgame's root GameObject should be active at a time; the
/// manager handles activating/deactivating them for you.
/// </summary>
public abstract class MicrogameBase : MonoBehaviour, IMicrogame
{
    [Header("Microgame Info")]
    [SerializeField] private string instruction = "DO IT!";
    [SerializeField] private float baseTimeLimit = 4f; // 4 seconds - on theme!

    public string Instruction => instruction;
    public float BaseTimeLimit => baseTimeLimit;

    public event Action<bool> OnMicrogameResult;

    private bool hasReported;

    public virtual void StartMicrogame()
    {
        hasReported = false;
    }

    public virtual void TickMicrogame(float deltaTime) { }

    public virtual void ForceStop()
    {
        // Override to disable colliders/input if the microgame needs cleanup
        // when it times out without a result (counts as a loss upstream).
    }

    /// <summary>
    /// Call this from your microgame's own logic the instant a win or
    /// lose condition happens (e.g. player tapped 4 times, or missed).
    /// Safe to call only once per round; extra calls are ignored.
    /// </summary>
    protected void ReportResult(bool won)
    {
        if (hasReported) return;
        hasReported = true;
        OnMicrogameResult?.Invoke(won);
    }
}
