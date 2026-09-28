using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{
    public int lives;

    private float playerSpeed;

    private float horizontalInput;

    private float verticalInput;

    private float halfY;

    private float bottomY;

    private float horizontalScreenLimit = 9.5f;

    private float verticalScreenLimit = 6.5f;

    public GameObject explosionPrefab;

    public GameObject bulletPrefab;

    private GameManager gameManager;


    void Start()
    {
        playerSpeed = 6f;
        lives = 3;

        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        gameManager.ChangeLivesText(lives);
    }

    public void LoseALife()
    {
        Debug.Log("Player hit! Lose a life.");
        lives--;
        gameManager.ChangeLivesText(lives);
        if (lives == 0)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(this.gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        Movement();
        Shooting();

    }

    void Movement()
    {
        //get player input
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        //move the player
        transform.Translate(new Vector3(horizontalInput, verticalInput, 0) * Time.deltaTime * playerSpeed);

        //player can leave the screen horizontally
        if(transform.position.x > horizontalScreenLimit || transform.position.x < -horizontalScreenLimit)
        {
            transform.position = new Vector3(transform.position.x * -1, transform.position.y, 0);
        }

        //finds the halfway point of the screen
        float halfY = Camera.main.ScreenToWorldPoint(new Vector3(0, Screen.height / 2f, Camera.main.transform.position.z * -1)).y;
        //finds the bottom point of the screen
        float bottomY = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Camera.main.transform.position.z * -1)).y;

        // clamp position between bottom and halfway point
        Vector3 pos = transform.position;

        if(pos.y > halfY)
        {
            pos.y = halfY;
        }
        
        if(pos.y < bottomY)
        {
            pos.y = bottomY;
        }
        
        transform.position = pos;
    }
    
    void Shooting()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Pew Pew" + verticalScreenLimit);
            //spawn bullet at player position
            Instantiate(bulletPrefab, transform.position + new Vector3(0, 1, 0), Quaternion.identity);
        }
    }
}
