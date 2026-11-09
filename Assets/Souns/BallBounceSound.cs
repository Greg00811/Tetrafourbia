using UnityEngine;

/// <summary>
/// Put this on the ball prefab. Plays the bounce sound whenever the ball
/// collides with something (the paddle, other balls). BallJuggle gives
/// itself to the ball when it spawns, so nothing needs assigning.
/// </summary>
public class BallBounceSound : MonoBehaviour
{
    [Tooltip("Ignore soft touches below this speed, so a ball resting on something stays quiet.")]
    [SerializeField] private float minImpactSpeed = 1f;

    private BallJuggle game;

    public void Init(BallJuggle owner)
    {
        game = owner;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (game == null) return;

        if (collision.relativeVelocity.magnitude >= minImpactSpeed)
            game.PlayBounceSound();
    }
}