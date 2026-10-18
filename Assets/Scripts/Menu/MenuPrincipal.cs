using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    public TMP_Text mensajeEstado;

    private bool cargando;

    private void Awake()
    {
        Time.timeScale = 1f;
        LimpiarMensaje();
    }

    public void AbrirNivel(string nombreEscena)
    {
        if (cargando)
            return;

        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            MostrarMensaje("Revisa la escena en Build Settings");

            Debug.LogWarning(
                "No se puede cargar la escena: " + nombreEscena,
                this);

            return;
        }

        cargando = true;
        SceneManager.LoadScene(nombreEscena);
    }

    public void MostrarProximamente()
    {
        MostrarMensaje("Disponible pronto");
    }

    private void MostrarMensaje(string texto)
    {
        if (mensajeEstado == null)
            return;

        mensajeEstado.text = texto;

        CancelInvoke(nameof(LimpiarMensaje));
        Invoke(nameof(LimpiarMensaje), 2.5f);
    }

    private void LimpiarMensaje()
    {
        if (mensajeEstado != null)
            mensajeEstado.text = "";
    }
}