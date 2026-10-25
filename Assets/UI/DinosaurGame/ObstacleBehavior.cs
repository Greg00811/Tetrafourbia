using UnityEngine;

public class ObstacleBehavior : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float despawnLimit;

    [SerializeField] private DinosaurGame dinosaurGame;

    private void Start()
    {
        dinosaurGame = GameObject.Find("DinosaurGame").GetComponent<DinosaurGame>();
    }
    void Update()
    {
        transform.position += Vector3.left * Time.deltaTime * moveSpeed;

        if (transform.position.x < despawnLimit)
        {
            dinosaurGame.obstaclesDestroyed++;
            Destroy(this.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("DinosaurGamePlayer"))
        {
            dinosaurGame.FailState();
            //DinosaurGame dinosaurGame = gameObject<DinosaurGame>();
        }
    }
}
