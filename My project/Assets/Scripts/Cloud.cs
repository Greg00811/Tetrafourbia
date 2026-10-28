using UnityEngine;

public class Cloud : MonoBehaviour
{
    private float speed;

    private GameManager gameManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>(); //look at hierarchy get the game manager object and get its script

        transform.localScale = transform.localScale * Random.Range(0.1f, 0.6f);
        transform.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, Random.Range(0.1f, 0.7f)); //the last one alpha is transparency
        speed = Random.Range(3f, 7f);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.down * speed * Time.deltaTime); //move down at speed
        if(transform.position.y < -gameManager.verticalScreenSize)
        {
            transform.position = new Vector3(Random.Range(-gameManager.horizontalScreenSize, gameManager.horizontalScreenSize), gameManager.verticalScreenSize * 1.2f, 0);
        }
    }
}
