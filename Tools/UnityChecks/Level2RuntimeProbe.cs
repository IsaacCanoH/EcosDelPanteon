using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class Level2RuntimeProbe:MonoBehaviour
{
    readonly List<string> results=new List<string>();
    readonly List<float> launches=new List<float>();
    readonly HashSet<int> seen=new HashSet<int>();
    bool finished;
    void Update()
    {
        foreach(var stone in FindObjectsOfType<GhostStoneProjectile>())if(seen.Add(stone.GetInstanceID()))launches.Add(Time.time);
    }
    void Check(bool value,string message)
    {
        if(!value) { results.Add("FAIL: "+message);Finish(1);throw new Exception(message); }
        results.Add("OK: "+message);
    }
    void Finish(int code)
    {
        if(finished)return;finished=true;
        File.WriteAllLines("Documentation/Nivel2-pruebas-jugables.txt",results);
        Debug.Log("PLAY CHECKS: "+string.Join("\n",results));
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#endif
    }
    IEnumerator Start()
    {
        Time.timeScale=3;
        yield return null;
        yield return new WaitForSeconds(.2f);
        var player=GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Level2CharacterController>().enabled=false;
        var rb=player.GetComponent<Rigidbody2D>();
        rb.interpolation=RigidbodyInterpolation2D.None;
        CaptureCamera();
        Check(player.GetComponent<SpriteRenderer>().enabled&&player.GetComponent<SpriteRenderer>().sprite!=null,"Sprite del jugador activo en modo de juego");
        var health=player.GetComponent<PlayerHealth>();health.maxHealth=100;health.currentHealth=100;
        var toys=player.GetComponent<ToyCollector>();
        var ai=FindObjectOfType<GhostChildAI>();var enemyBody=ai.GetComponent<Rigidbody2D>();
        rb.position=new Vector2(0,-3);enemyBody.position=new Vector2(0,-6.5f);Physics2D.SyncTransforms();
        yield return new WaitForSeconds(1);
        Check(ai.CurrentPhase==GhostChildAI.Phase.Watching&&health.currentHealth==100,"Sin juguetes: sigue a distancia y no inflige daño");
        Check(Vector2.Distance(rb.position,enemyBody.position)>3.5f,"El acecho mantiene separación");
        Check(toys.Collect(0)&&!toys.Collect(0)&&toys.Count==1,"Cada juguete sólo se cuenta una vez");
        enemyBody.position=rb.position+Vector2.down*.6f;Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.9f);
        Check(ai.CurrentPhase==GhostChildAI.Phase.Melee&&health.currentHealth<100,"Primer juguete: golpe cuerpo a cuerpo con daño");
        toys.Collect(1);rb.position=new Vector2(0,-1);enemyBody.position=new Vector2(0,-6);Physics2D.SyncTransforms();
        yield return new WaitForSeconds(9.1f);
        Check(ai.CurrentPhase==GhostChildAI.Phase.MediumRange&&launches.Count>=2,"Dos juguetes: lanza piedras desde distancia media");
        Check(health.currentHealth<99,"Las piedras pueden dañar al jugador");
        int mediumLaunches=launches.Count;
        toys.Collect(2);rb.position=Vector2.zero;enemyBody.position=new Vector2(0,-8.5f);Physics2D.SyncTransforms();
        yield return new WaitForSeconds(8.5f);
        Check(ai.CurrentPhase==GhostChildAI.Phase.LongRange&&launches.Count>=mediumLaunches+2,"Tres juguetes: permanece activo y lanza a distancia larga");
        Check(launches.Zip(launches.Skip(1),(a,b)=>b-a).All(t=>t>=3.97f),"Todos los lanzamientos están separados por al menos 4 segundos, incluido el cambio de fase");
        ai.enabled=false;
        foreach(var stone in FindObjectsOfType<GhostStoneProjectile>())Destroy(stone.gameObject);
        yield return null;
        var wall=new GameObject("Pared de prueba");wall.layer=6;wall.transform.position=new Vector2(0,-4.5f);wall.AddComponent<BoxCollider2D>().size=new Vector2(2,.3f);
        rb.position=new Vector2(0,-3);rb.velocity=Vector2.zero;Physics2D.SyncTransforms();
        int before=health.currentHealth;
        var projectile=Instantiate(ai.stonePrefab,new Vector3(0,-6,0),Quaternion.identity);
        projectile.Launch(rb.position,ai.GetComponentsInChildren<Collider2D>(),1);
        yield return new WaitForSeconds(.9f);
        Check(projectile==null&&health.currentHealth==before,"Las piedras chocan contra obstáculos y no dañan a través de ellos");
        Destroy(wall);
        var props=GameObject.Find("SCENE/LAYER 1/Props");
        foreach(string token in new[]{"Tree 01","Stone Bench 01 S"})
        {
            var obstacle=props.GetComponentsInChildren<BoxCollider2D>().First(c=>c.name.Contains(token)&&!c.isTrigger);
            var playerFeet=player.GetComponent<BoxCollider2D>();
            Vector2 offset=Vector2.Scale(playerFeet.offset,player.transform.lossyScale);
            rb.position=(Vector2)obstacle.bounds.center+Vector2.down*(obstacle.bounds.extents.y+.8f)-offset;
            rb.velocity=Vector2.up*3;Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.6f);rb.velocity=Vector2.zero;
            var contact=playerFeet.Distance(obstacle);
            results.Add("CONTACTO "+token+": distancia="+contact.distance+", jugadorY="+playerFeet.bounds.center.y+", obstaculoY="+obstacle.bounds.center.y);
            Check(contact.distance>=-.02f && playerFeet.bounds.center.y<obstacle.bounds.center.y,"Colisión física impide atravesar "+token);
            var d=obstacle.GetComponent<WorldDepth2D>();var pd=player.GetComponent<WorldDepth2D>();
            player.transform.position=obstacle.transform.position+Vector3.up;Physics2D.SyncTransforms();pd.Refresh();d.Refresh();
            Check(player.GetComponent<SortingGroup>().sortingOrder<obstacle.GetComponent<SortingGroup>().sortingOrder,"Detrás de "+token+": jugador dibujado al fondo");
            player.transform.position=obstacle.transform.position+Vector3.down;Physics2D.SyncTransforms();pd.Refresh();
            Check(player.GetComponent<SortingGroup>().sortingOrder>obstacle.GetComponent<SortingGroup>().sortingOrder,"Frente a "+token+": jugador dibujado delante");
        }
        Check(FindObjectsOfType<GhostChildAI>().Length==1,"El niño no desaparece al recuperar el tercer juguete");
        Finish(0);
    }
    void CaptureCamera()
    {
        var camera=Camera.main;var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();
        var old=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes("Documentation/Nivel2-inicio.png",texture.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;target.Release();Destroy(target);Destroy(texture);
    }
}
