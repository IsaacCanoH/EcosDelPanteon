using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string KeyMaster = "Audio_MasterVolume";
    public const string KeyEnemy = "Audio_EnemyVolume";
    public const string KeyMusic = "Audio_MusicVolume";

    [Header("Volúmenes Actuales")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float enemyVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;

    [Header("Fuentes de Audio")]
    public AudioSource menuMusicSource;
    public AudioSource sfxSource;

    [Header("Clips de Referencia")]
    public AudioClip menuMusicClip;
    public AudioClip enemyPreviewClip;
    public AudioClip buttonClickClip;

    public static float MasterVolume
    {
        get => Instance != null ? Instance.masterVolume : PlayerPrefs.GetFloat(KeyMaster, 1f);
        set { if (Instance != null) Instance.SetMasterVolume(value); }
    }

    public static float EnemyVolume
    {
        get => Instance != null ? Instance.enemyVolume : PlayerPrefs.GetFloat(KeyEnemy, 0.8f);
        set { if (Instance != null) Instance.SetEnemyVolume(value); }
    }

    public static float MusicVolume
    {
        get => Instance != null ? Instance.musicVolume : PlayerPrefs.GetFloat(KeyMusic, 0.7f);
        set { if (Instance != null) Instance.SetMusicVolume(value); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Cargar preferencias guardadas
        masterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        enemyVolume = PlayerPrefs.GetFloat(KeyEnemy, 0.8f);
        musicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.7f);

        // Crear fuentes de audio internas
        if (menuMusicSource == null)
        {
            menuMusicSource = gameObject.AddComponent<AudioSource>();
            menuMusicSource.loop = true;
            menuMusicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        LoadAudioAssets();
        ApplyAllVolumes();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LoadAudioAssets()
    {
#if UNITY_EDITOR
        if (menuMusicClip == null)
            menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Lacrimosa.mp3");
        if (enemyPreviewClip == null)
            enemyPreviewClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Goblin/duende_sonido_avistamiento.wav");
        if (buttonClickClip == null)
            buttonClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Player/Piedras/Piedra_Impacto.ogg");
#endif
        if (menuMusicClip == null)
            menuMusicClip = Resources.Load<AudioClip>("Audio/Lacrimosa");
        if (enemyPreviewClip == null)
            enemyPreviewClip = Resources.Load<AudioClip>("Audio/duende_sonido_avistamiento");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAllVolumes();

        // En el menú principal, reproducir música de fondo si no está sonando
        if (scene.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PlayMenuMusic();
        }
        else
        {
            // En niveles de juego, detener música de menú y actualizar música de la escena
            if (menuMusicSource != null && menuMusicSource.isPlaying)
            {
                menuMusicSource.Stop();
            }

            UpdateSceneBackgroundMusic();
            UpdateSceneEnemies();
        }
    }

    public void PlayMenuMusic()
    {
        if (menuMusicSource == null) return;
        if (menuMusicClip != null && menuMusicSource.clip != menuMusicClip)
        {
            menuMusicSource.clip = menuMusicClip;
        }

        menuMusicSource.volume = musicVolume * 0.55f;
        if (!menuMusicSource.isPlaying && menuMusicSource.clip != null)
        {
            menuMusicSource.Play();
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(KeyMaster, masterVolume);
        PlayerPrefs.Save();
    }

    public void SetEnemyVolume(float value)
    {
        enemyVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyEnemy, enemyVolume);
        PlayerPrefs.Save();
        UpdateSceneEnemies();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyMusic, musicVolume);
        PlayerPrefs.Save();

        if (menuMusicSource != null)
        {
            menuMusicSource.volume = musicVolume * 0.55f;
        }

        UpdateSceneBackgroundMusic();
    }

    public void ApplyAllVolumes()
    {
        AudioListener.volume = masterVolume;

        if (menuMusicSource != null)
        {
            menuMusicSource.volume = musicVolume * 0.55f;
        }

        UpdateSceneBackgroundMusic();
        UpdateSceneEnemies();
    }

    public void UpdateSceneBackgroundMusic()
    {
        GameObject bgm = GameObject.Find("BackgroundMusic");
        if (bgm != null)
        {
            AudioSource src = bgm.GetComponent<AudioSource>();
            if (src != null)
            {
                src.volume = musicVolume * 0.85f;
            }
        }
    }

    public void UpdateSceneEnemies()
    {
        // Actualizar duendes activos
        GoblinAudio[] goblins = FindObjectsOfType<GoblinAudio>();
        foreach (var goblin in goblins)
            goblin.ActualizarMezcla();
    }

    public void PlayPreviewEnemySound()
    {
        if (sfxSource == null || enemyPreviewClip == null) return;
        sfxSource.PlayOneShot(enemyPreviewClip, 0.9f * enemyVolume);
    }

    public void PlayButtonClick()
    {
        if (sfxSource == null || buttonClickClip == null) return;
        sfxSource.PlayOneShot(buttonClickClip, 0.45f);
    }
}

