using System;

/// <summary>
/// Every microgame in the collection implements this interface.
/// The MicrogameManager talks to microgames only through this contract,
/// so you can add as many microgame prefabs/scenes as you want without
/// touching the manager code.
/// </summary>
public interface IMicrogame
{
    /// <summary>
    /// Short instruction shown to the player before the game starts,
    /// WarioWare-style ("TAP 4x!", "DODGE!", "CATCH FOUR!").
    /// </summary>
    string Instruction { get; }

    /// <summary>
    /// How long (in seconds) this microgame gets to be completed.
    /// The manager may shrink this as difficulty ramps up.
    /// </summary>
    float BaseTimeLimit { get; }

    /// <summary>
    /// Called by the manager when this microgame becomes active.
    /// Use this to reset state and enable whatever the player interacts with.
    /// </summary>
    void StartMicrogame();

    /// <summary>
    /// Called every frame while this microgame is active, in case it
    /// needs manual ticking (most logic can just live in Unity's own Update
    /// on the microgame's MonoBehaviour instead, this is optional).
    /// </summary>
    void TickMicrogame(float deltaTime);

    /// <summary>
    /// Called by the manager when time runs out and the microgame never
    /// reported a result. Use this to disable input/visuals cleanly.
    /// </summary>
    void ForceStop();

    /// <summary>
    /// Fired by the microgame itself the moment a win/lose condition is met.
    /// The manager subscribes to this to know when to move on.
    /// </summary>
    event Action<bool> OnMicrogameResult; // true = win, false = lose
}
