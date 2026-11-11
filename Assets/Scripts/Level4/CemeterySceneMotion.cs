using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CemeterySceneMotion
{
    internal static void Place(PlayerHealth player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.velocity = Vector2.zero;
        body.position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    internal static IEnumerator Walk(PlayerHealth player, Vector2 destination, float speed)
    {
        var body = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();
        var sprite = player.GetComponent<SpriteRenderer>();
        var animatorMode = animator.updateMode;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        var direction = destination - body.position;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            animator.SetInteger("Direction", direction.x < 0f ? 3 : 2);
            sprite.flipX = direction.x < 0f;
        }
        else
        {
            animator.SetInteger("Direction", direction.y > 0f ? 1 : 0);
            sprite.flipX = false;
        }

        animator.SetBool("IsMoving", true);
        while (Vector2.Distance(body.position, destination) > 0.025f)
        {
            Vector2 next = Vector2.MoveTowards(body.position, destination, speed * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            body.position = next;
            player.transform.position = next;
            yield return null;
        }

        Place(player, destination);
        animator.SetBool("IsMoving", false);
        animator.updateMode = animatorMode;
    }

    internal static IEnumerator WalkPath(PlayerHealth player, GoblinNavigation2D nav, Vector2 destination)
    {
        var feet = player.GetComponent<BoxCollider2D>();
        Vector2 offset = feet.transform.TransformVector(feet.offset);
        Vector2 start = (Vector2)player.transform.position + offset;
        Vector2 goal = destination + offset;
        var path = new List<Vector2>();
        if (!nav.FindPath(start, goal, path) || path.Count == 0 ||
            Vector2.Distance(path[path.Count - 1], goal) > 0.45f)
        {
            Debug.LogWarning("No se encontró la ruta cinematográfica del panteón.", player);
            yield break;
        }

        for (int i = 0; i < path.Count;)
        {
            int next = i;
            while (next + 1 < path.Count && nav.Clear(start, path[next + 1])) next++;
            start = path[next];
            yield return Walk(player, start - offset, 3.65f);
            i = next + 1;
        }
    }

    internal static IEnumerator Pan(Camera camera, Vector2 center, float zoom, float duration)
    {
        Vector3 start = camera.transform.position;
        Vector3 destination = new Vector3(center.x, center.y, start.z);
        float startingZoom = camera.orthographicSize;
        for (float t = 0f; t < duration; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            camera.transform.position = Vector3.Lerp(start, destination, progress);
            camera.orthographicSize = Mathf.Lerp(startingZoom, zoom, progress);
            yield return null;
        }

        camera.transform.position = destination;
        camera.orthographicSize = zoom;
    }

    internal static IEnumerator Iris(IrisInNivel iris, Transform player, bool close, float duration)
    {
        iris.gameObject.SetActive(false);
        iris.closing = close;
        iris.duracion = duration;
        iris.esperaInicial = 0f;
        iris.jugador = player;
        iris.gameObject.SetActive(true);
        while (!iris.IsComplete) yield return null;
    }
}

internal static class CemeteryEpilogueRoute
{
    private static bool pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => pending = false;

    internal static bool Consume()
    {
        bool requested = pending;
        pending = false;
        return requested;
    }

    internal static void ReturnToFirstCemetery()
    {
        pending = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("Cementerio_Nivel1");
    }
}
