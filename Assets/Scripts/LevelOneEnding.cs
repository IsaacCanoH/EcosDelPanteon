using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-120)]
public class LevelOneEnding : MonoBehaviour
{
    public enum EndingState { Playing, VictoryMessage, WalkingOut, FadingOut, Finished, Defeat, Paused }
    public GoblinWaveDirector director;
    public PlayerHealth health;
    public CoinSearchUI clues;
    public GameObject gameplayHUD;
    public GoblinNavigation2D exitNavigation;
    public Transform gatePoint;
    public Transform offscreenPoint;
    public Cainos.PixelArtTopDown_Basic.CameraFollow cameraFollow;
    public Camera exitCamera;
    public AudioSource music;
    public CanvasGroup resultPanel;
    public CanvasGroup resultBackdrop;
    public CanvasGroup blackout;
    public VictoryCelebration victoryCelebration;
    public TMP_Text resultTitle;
    public TMP_Text resultText;
    public TMP_Text resultHint;
    public GameObject buttonRow;
    public Button retryButton;
    public Button menuButton;
    public Button quitButton;
    [Min(0.5f)] public float victoryMessageSeconds = 2.5f;
    [Min(0.1f)] public float fadeSeconds = 1f;
    [Min(0.1f)] public float exitSpeed = 3f;
    public string menuScene = "MenuPrincipal";

    public EndingState State { get; private set; }
    public bool ExitFinished { get; private set; }
    private TopDownCharacterController movement;
    private PlayerCoinSearch search;
    private Rigidbody2D body;
    private BoxCollider2D playerCollider;
    private Animator animator;
    private SpriteRenderer playerVisual;
    private readonly List<Vector2> route = new List<Vector2>();
    private int routeIndex;
    private bool walking;
    private bool trackExitCamera;
    private bool loading;
    private bool ownsPause;
    private float musicVolume;
    private float pausedTimeScale;
    private bool pausedAudio;
    private bool pausedMovement;
    private GameObject previousSelection;
    private TMP_Text retryLabel;
    private IrisInNivel closingIris;

    private void Awake()
    {
        movement = health.GetComponent<TopDownCharacterController>();
        search = health.GetComponent<PlayerCoinSearch>();
        body = health.GetComponent<Rigidbody2D>();
        playerCollider = health.GetComponent<BoxCollider2D>();
        animator = health.GetComponent<Animator>();
        playerVisual = health.GetComponent<SpriteRenderer>();
        musicVolume = music != null ? music.volume : 0f;
        retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
        HideGroup(resultPanel);
        HideGroup(resultBackdrop);
        HideGroup(blackout);
        closingIris = blackout.GetComponent<IrisInNivel>();
        if (closingIris != null) closingIris.enabled = false;
        if (victoryCelebration != null) victoryCelebration.Hide();
        buttonRow.SetActive(false);
    }

    private void OnEnable()
    {
        retryButton.onClick.AddListener(Retry);
        menuButton.onClick.AddListener(ReturnToMenu);
        quitButton.onClick.AddListener(QuitGame);
    }

    private static void HideGroup(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (GameControls.PausePressed && !loading)
        {
            if (State == EndingState.Paused) Resume();
            else if (State == EndingState.Playing && health.currentHealth > 0 &&
                !director.IsComplete &&
                (clues == null || clues.opening == null || !clues.opening.isActiveAndEnabled))
                OpenPauseMenu();
        }
        if (State != EndingState.Playing || loading) return;
        if (health.currentHealth <= 0)
        {
            State = EndingState.Defeat;
            LockGameplay();
            ShowResult(false, true);
            PauseResult();
        }
        else if (director.IsComplete)
        {
            State = EndingState.VictoryMessage;
            LockGameplay();
            StartCoroutine(VictorySequence());
        }
    }

    public void OpenPauseMenu()
    {
        if (State != EndingState.Playing || loading || health.currentHealth <= 0 ||
            director.IsComplete) return;
        if (search != null) search.InterruptSearch();
        pausedMovement = movement != null && movement.enabled;
        if (pausedMovement) movement.enabled = false;
        body.velocity = Vector2.zero;
        animator.SetBool("IsMoving", false);
        pausedTimeScale = Time.timeScale;
        pausedAudio = AudioListener.pause;
        previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        if (clues != null) clues.SetMenuCovered(true);
        State = EndingState.Paused;
        resultTitle.text = "PAUSA";
        resultText.text = "El juego está en pausa.\n" +
            "<link=\"key:ESC\"><color=#00000000> ESC </color></link> para continuar.";
        resultHint.gameObject.SetActive(false);
        retryLabel.text = "CONTINUAR";
        buttonRow.SetActive(true);
        resultPanel.alpha = 1f;
        resultPanel.interactable = true;
        resultPanel.blocksRaycasts = true;
        resultBackdrop.alpha = 1f;
        resultBackdrop.blocksRaycasts = true;
        PauseResult();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    public void Resume()
    {
        if (State != EndingState.Paused) return;
        HideGroup(resultPanel);
        HideGroup(resultBackdrop);
        buttonRow.SetActive(false);
        State = EndingState.Playing;
        ownsPause = false;
        Time.timeScale = pausedTimeScale;
        AudioListener.pause = pausedAudio;
        if (pausedMovement && movement != null) movement.enabled = true;
        pausedMovement = false;
        if (clues != null) clues.SetMenuCovered(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void LockGameplay()
    {
        if (clues != null) clues.CloseClue();
        if (search != null)
        {
            search.InterruptSearch();
            search.enabled = false;
        }
        if (movement != null) movement.enabled = false;
        body.velocity = Vector2.zero;
        animator.SetBool("IsMoving", false);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void ShowResult(bool victory, bool buttons)
    {
        resultBackdrop.transform.SetAsLastSibling();
        resultPanel.transform.SetAsLastSibling();
        retryLabel.text = "REINTENTAR";
        resultTitle.text = victory ? "VICTORIA" : "DERROTA";
        resultText.text = victory
            ? "Recuperaste las tres monedas.\nEl panteón vuelve a estar en calma."
            : "Los duendes te han alcanzado.\nVuelve a intentarlo.";
        resultHint.text = "ABANDONANDO EL PANTEÓN...";
        resultHint.gameObject.SetActive(!buttons);
        buttonRow.SetActive(buttons);
        resultPanel.alpha = 1f;
        resultBackdrop.alpha = 1f;
        resultPanel.interactable = buttons;
        resultPanel.blocksRaycasts = true;
        resultBackdrop.blocksRaycasts = true;
        if (buttons && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    private IEnumerator VictorySequence()
    {
        ShowResult(true, false);
        if (victoryCelebration != null) victoryCelebration.Show();
        yield return new WaitForSecondsRealtime(victoryMessageSeconds);
        yield return FadeResultsOut();
        if (!PrepareExitRoute())
        {
            Debug.LogWarning("No se pudo preparar la ruta de salida del nivel.", this);
            yield return FadeToBlack();
            CompleteExit();
            yield break;
        }
        State = EndingState.WalkingOut;
        if (cameraFollow != null) cameraFollow.enabled = false;
        trackExitCamera = true;
        walking = true;
        float deadline = Time.time + RouteLength() / exitSpeed * 2f + 4f;
        while (walking && Time.time < deadline) yield return null;
        if (walking)
        {
            walking = false;
            Debug.LogWarning("La salida no pudo terminar su recorrido.", this);
        }
        body.velocity = Vector2.zero;
        animator.SetBool("IsMoving", false);
        yield return FadeToBlack();
        CompleteExit();
    }

    private IEnumerator FadeResultsOut()
    {
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / 0.25f);
            resultPanel.alpha = alpha;
            resultBackdrop.alpha = alpha;
            yield return null;
        }
        HideGroup(resultPanel);
        HideGroup(resultBackdrop);
    }

    private bool PrepareExitRoute()
    {
        route.Clear();
        routeIndex = 0;
        if (exitNavigation == null || gatePoint == null || offscreenPoint == null) return false;
        Vector2 offset = (Vector2)playerCollider.bounds.center - body.position;
        Vector2 cursor = body.position + offset;
        Vector2 goal = (Vector2)gatePoint.position + offset;
        var centers = new List<Vector2>();
        if (!exitNavigation.FindPath(cursor, goal, centers) || centers.Count == 0 ||
            Vector2.Distance(centers[centers.Count - 1], goal) > 0.35f)
            return false;
        int next = 0;
        while (next < centers.Count)
        {
            int farthest = -1;
            for (int i = centers.Count - 1; i >= next; i--)
                if (exitNavigation.Clear(cursor, centers[i])) { farthest = i; break; }
            if (farthest < 0) return false;
            cursor = centers[farthest];
            route.Add(cursor - offset);
            next = farthest + 1;
        }
        route.Add(gatePoint.position);
        return true;
    }

    private float RouteLength()
    {
        float length = 0f;
        Vector2 previous = body.position;
        foreach (Vector2 point in route) { length += Vector2.Distance(previous, point); previous = point; }
        return length + Vector2.Distance(gatePoint.position, offscreenPoint.position);
    }

    private void FixedUpdate()
    {
        if (!walking || State != EndingState.WalkingOut) return;
        Vector2 target = routeIndex < route.Count ? route[routeIndex] : (Vector2)offscreenPoint.position;
        if (Vector2.Distance(body.position, target) < 0.035f)
        {
            if (routeIndex < route.Count)
            {
                routeIndex++;
                if (routeIndex == route.Count)
                {
                    // Fuera de la zona jugable, continuar hasta desaparecer del encuadre.
                    playerCollider.enabled = false;
                    trackExitCamera = false;
                    if (cameraFollow != null) cameraFollow.enabled = false;
                    playerVisual.enabled = true;
                }
            }
            else
            {
                walking = false;
                return;
            }
        }
        target = routeIndex < route.Count ? route[routeIndex] : (Vector2)offscreenPoint.position;
        Vector2 direction = target - body.position;
        if (direction.sqrMagnitude < 0.000001f) return;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            animator.SetInteger("Direction", direction.x < 0f ? 3 : 2);
            playerVisual.flipX = direction.x < 0f;
        }
        else
        {
            animator.SetInteger("Direction", direction.y > 0f ? 1 : 0);
            playerVisual.flipX = false;
        }
        animator.SetBool("IsMoving", true);
        body.velocity = Vector2.zero;
        body.MovePosition(Vector2.MoveTowards(body.position, target, exitSpeed * Time.fixedDeltaTime));
    }

    private void LateUpdate()
    {
        if (!trackExitCamera || exitCamera == null) return;
        Vector3 position = exitCamera.transform.position;
        exitCamera.transform.position = new Vector3(body.position.x, body.position.y, position.z);
    }

    private IEnumerator FadeToBlack()
    {
        State = EndingState.FadingOut;
        blackout.transform.SetAsLastSibling();
        blackout.blocksRaycasts = true;
        if (closingIris != null)
        {
            closingIris.duracion = fadeSeconds;
            blackout.alpha = 1f;
            closingIris.enabled = true;
            while (!closingIris.IsComplete)
            {
                if (music != null) music.volume = musicVolume * (1f - closingIris.Progress);
                yield return null;
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeSeconds);
                blackout.alpha = progress;
                if (music != null) music.volume = musicVolume * (1f - progress);
                yield return null;
            }
        }
        blackout.alpha = 1f;
        if (music != null) music.volume = 0f;
    }

    private void CompleteExit()
    {
        if (victoryCelebration != null) victoryCelebration.Hide();
        ExitFinished = true;
        State = EndingState.Finished;
        if (Application.isEditor)
        {
            ShowResult(true, true);
            PauseResult();
        }
        else Application.Quit();
    }

    private void PauseResult()
    {
        ownsPause = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void RestoreClock()
    {
        ownsPause = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void Retry()
    {
        if (State == EndingState.Paused) { Resume(); return; }
        if (loading || (State != EndingState.Defeat && State != EndingState.Finished)) return;
        loading = true;
        RestoreClock();
        SceneManager.LoadScene(gameObject.scene.name);
    }

    public void ReturnToMenu()
    {
        if (loading || (State != EndingState.Defeat && State != EndingState.Finished &&
            State != EndingState.Paused)) return;
        if (State == EndingState.Paused) Resume();
        if (clues != null) clues.CloseClue();
        loading = true;
        RestoreClock();
        SceneManager.LoadScene(menuScene);
    }

    public void QuitGame()
    {
        if (State != EndingState.Defeat && State != EndingState.Finished &&
            State != EndingState.Paused) return;
        if (Application.isEditor) ReturnToMenu();
        else Application.Quit();
    }

    private void OnDisable()
    {
        if (victoryCelebration != null) victoryCelebration.Hide();
        retryButton.onClick.RemoveListener(Retry);
        menuButton.onClick.RemoveListener(ReturnToMenu);
        quitButton.onClick.RemoveListener(QuitGame);
        walking = false;
        if (State == EndingState.Paused)
        {
            Resume();
            if (clues != null) clues.CloseClue();
            RestoreClock();
        }
        if (ownsPause) RestoreClock();
    }
}
