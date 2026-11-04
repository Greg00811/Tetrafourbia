using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource menuMusic;

    public static AudioManager Instance; //Allows access to Audio Manager from other scenes

    public float musicVolume = 1f; //Current value of music



    //Checks for Audio Manager and makes it persist through changing of scenes
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //Changes volume based on slider value
    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        menuMusic.volume = musicVolume;

        Debug.Log("Slider Value: " + volume);
        Debug.Log("AudioSource Volume: " + menuMusic.volume);
    }
}
