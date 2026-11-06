using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource menuMusic;
    private Slider musicSlider;
    private AudioSource gameMusic;

    public static AudioManager Instance; //Allows access to Audio Manager from other scenes

    public float musicVolume = 1f; //Current value of music



    //Checks for Audio Manager and makes it persist through changing of scenes
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupMainMenu();
    }

    //Detects if in game scene and finds audio source component
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + scene.name);
        Debug.Log("Saved music volume: " + musicVolume);

        if (scene.name == "MicrogameLoop")
        {
            gameMusic = GameObject.Find("Main Camera").GetComponent<AudioSource>();
            Debug.Log("Game music found: " + gameMusic);

            gameMusic.volume = musicVolume;
        }

        if (scene.name == "MainMenu")
        {
            SetupMainMenu();
        }
    }

    private void SetupMainMenu()
    {
        menuMusic = GameObject.Find("Canvas").GetComponent<AudioSource>();
        menuMusic.volume = musicVolume;

        musicSlider = GameObject.Find("Canvas").GetComponentInChildren<Slider>(true);

        musicSlider.SetValueWithoutNotify(musicVolume);
    }

    //Changes volume based on slider value
    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;

        if (menuMusic != null)
        {
            menuMusic.volume = musicVolume;
        }

        if (gameMusic != null)
        {
            gameMusic.volume = musicVolume;
        }
    }
}
