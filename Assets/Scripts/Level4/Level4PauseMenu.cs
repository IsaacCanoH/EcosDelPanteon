using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-120)]
public sealed class Level4PauseMenu : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Level4StoryEnding ending;
    [SerializeField] private CanvasGroup card;
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text message;
    [SerializeField] private GameObject hint;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button quitButton;

    private enum View { Hidden, Paused, Victory }
    private View view;
    private Cainos.PixelArtTopDown_Basic.TopDownCharacterController movement;
    private Rigidbody2D body;
    private Animator animator;
    private GameObject previousSelection;
    private float previousTime;
    private bool previousAudio;
    private bool restoreMovement;
    private bool ownsPause;
    private bool loading;

    private void Awake()
    {
        movement = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        body = player.GetComponent<Rigidbody2D>();
        animator = player.GetComponent<Animator>();
        if (hint != null) hint.SetActive(false);
        SetVisible(false);
        resumeButton.onClick.AddListener(Resume);
        menuButton.onClick.AddListener(ReturnToMenu);
        quitButton.onClick.AddListener(Quit);
    }

    private void Update()
    {
        if (loading || !GameControls.PausePressed) return;
        if (view == View.Paused) Resume();
        else if (view == View.Hidden) OpenPause();
    }

    private void OpenPause()
    {
        var parchment = ParchmentMessageUI.Instance;
        if (player.IsDead || ending.IsRunning || Time.timeScale <= 0f ||
            (parchment != null && (parchment.IsOpen || parchment.ChangedThisFrame)) ||
            movement == null || !movement.enabled)
            return;

        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        restoreMovement = movement.enabled;
        movement.enabled = false;
        body.velocity = Vector2.zero;
        animator.SetBool("IsMoving", false);
        view = View.Paused;
        title.text = "PAUSA";
        message.text = "El juego está en pausa.\n<link=\"key:ESC\"><color=#00000000> ESC </color></link> para continuar.";
        resumeButton.gameObject.SetActive(true);
        SetVisible(true);
        PauseClock();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void Resume()
    {
        if (view != View.Paused) return;
        view = View.Hidden;
        SetVisible(false);
        RestoreClock();
        if (restoreMovement && movement != null) movement.enabled = true;
        restoreMovement = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    internal void ShowVictory()
    {
        view = View.Victory;
        title.text = "¡VICTORIA!";
        message.text = "Los tres muñecos descansan en el altar.\nEl panteón vuelve a estar en calma.";
        resumeButton.gameObject.SetActive(false);
        SetVisible(true);
        PauseClock();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuButton.gameObject);
    }

    private void SetVisible(bool show)
    {
        card.alpha = backdrop.alpha = show ? 1f : 0f;
        card.interactable = show;
        card.blocksRaycasts = backdrop.blocksRaycasts = show;
    }

    private void PauseClock()
    {
        if (ownsPause) return;
        previousTime = Time.timeScale;
        previousAudio = AudioListener.pause;
        ownsPause = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void RestoreClock()
    {
        if (!ownsPause) return;
        ownsPause = false;
        Time.timeScale = previousTime;
        AudioListener.pause = previousAudio;
    }

    private void ReturnToMenu()
    {
        if (view == View.Hidden || loading) return;
        loading = true;
        RestoreClock();
        SceneManager.LoadScene("MenuPrincipal");
    }

    private void Quit()
    {
        if (view == View.Hidden || loading) return;
        if (Application.isEditor) ReturnToMenu();
        else Application.Quit();
    }

    private void OnDisable()
    {
        if (view == View.Paused) Resume();
        else RestoreClock();
    }
}
