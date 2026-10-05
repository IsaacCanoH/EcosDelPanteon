using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColisionadorPlayer : MonoBehaviour
{
    public static bool enPiso = false;
    // Start is called before the first frame update
    private void OnTriggerEnter2D(Collider2D collision)
    {
        enPiso = true;
        Debug.Log("Colision con Piso");
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log("Salio del Piso");
        enPiso = false;
    }
}
