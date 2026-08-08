using UnityEngine;

/// <summary>All game sounds in one place. PUT ON: empty "AudioManager" object in the scene.</summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    [Header("Music")]
    [SerializeField] private AudioClip lobbyMusic;
    [SerializeField] private AudioClip runDrums;
    [SerializeField] private AudioClip characterMusic;
    [SerializeField] private AudioClip loginMusic;
    [SerializeField] private AudioClip leaderboardMusic;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 0.5f;

    [Header("SFX clips")]
    [SerializeField] private AudioClip jumpMale;
    [SerializeField] private AudioClip jumpFemale;
    [SerializeField] private AudioClip slide;
    [SerializeField] private AudioClip deathMale;
    [SerializeField] private AudioClip deathFemale;
    [SerializeField] private AudioClip coin;
    [SerializeField] private AudioClip monsterRoar;
    [SerializeField] private AudioClip uiClick;
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

    private AudioSource musicSource;
    private AudioSource musicLayer2Source;
    private AudioSource sfxSource;

    private void Awake()
    {
        I = this;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicLayer2Source = gameObject.AddComponent<AudioSource>();
        musicLayer2Source.loop = true;
        musicLayer2Source.playOnAwake = false;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    private void Start()
    {
       
        if (FindObjectOfType<LobbyUI>() != null) PlayLobbyMusic();
    }

    public void PlayLobbyMusic() { PlayMusic(lobbyMusic); }
    public void PlayRunMusic() { PlayMusic(runDrums); }
    public void PlayCharacterMusic()
    {
        PlayMusic(characterMusic);

        if (runDrums != null && musicLayer2Source != null)
        {
            musicLayer2Source.clip = runDrums;
            musicLayer2Source.volume = musicVolume;
            musicLayer2Source.Play();
        }

    }
    public void PlayLoginMusic() { PlayMusic(loginMusic); }
    public void PlayLeaderboardMusic() { PlayMusic(leaderboardMusic); }

    public void StopMusic() { if (musicSource != null) musicSource.Stop(); }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayJump(bool female) { Play(female ? jumpFemale : jumpMale); }
    public void PlaySlide() { Play(slide); }
    public void PlayDeath(bool female) { Play(female ? deathFemale : deathMale); }
    public void PlayCoin() { Play(coin); }
    public void PlayRoar() { Play(monsterRoar); }
    public void PlayClick() { Play(uiClick); }

    private void Play(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
    }
}