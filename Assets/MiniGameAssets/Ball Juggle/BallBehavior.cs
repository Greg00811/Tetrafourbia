using UnityEngine;

public class BallBehavior : MonoBehaviour
{
    private int deathZoneLayer;
    void Update()
    {
        if (transform.position.y < deathZoneLayer)
        {
            Destroy(this.gameObject);
        }
    }
}
