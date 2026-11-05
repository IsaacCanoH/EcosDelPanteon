using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cainos.PixelArtTopDown_Basic
{
    public class Level2CharacterController : MonoBehaviour
    {
        [Min(0.5f)] public float speed = 3.0f;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private PlayerHealth health;
        private PlayerWeaponController weaponController;
        private Vector2 movement;

        private void Awake()
        {
            enabled = true;
            if (speed <= 0.1f) speed = 3.0f;

            weaponController = GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                weaponController = gameObject.AddComponent<PlayerWeaponController>();
            }

            EnsureLevel2SceneSetup();
        }

        private void Start()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<PlayerHealth>();
            if (speed <= 0.1f) speed = 3.0f;

            // Usar ordenamiento nativo por pivote Y de URP 2D (Layer 1, sortingOrder = 2) con SortingGroup
            var depth = GetComponent<WorldDepth2D>();
            if (depth != null) Destroy(depth);

            var group = GetComponent<SortingGroup>();
            if (group == null) group = gameObject.AddComponent<SortingGroup>();
            group.sortingLayerName = "Layer 1";
            group.sortingOrder = 2;

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "Layer 1";
                spriteRenderer.sortingOrder = 2;
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }

            Transform shadow = transform.Find("Shadow");
            if (shadow != null)
            {
                var shadowSr = shadow.GetComponent<SpriteRenderer>();
                if (shadowSr != null)
                {
                    shadowSr.sortingLayerName = "Layer 1";
                    shadowSr.sortingOrder = 1;
                    shadowSr.spriteSortPoint = SpriteSortPoint.Pivot;
                }
            }
        }

        private void EnsureLevel2SceneSetup()
        {
            // 1. Desactivar componentes exclusivos del Nivel 1 en el jugador
            var coinCollector = GetComponent<GoldCoinCollector>();
            if (coinCollector != null) coinCollector.enabled = false;
            var coinSearch = GetComponent<PlayerCoinSearch>();
            if (coinSearch != null) coinSearch.enabled = false;

            // 2. Asegurar alcance del farol igual al Nivel 1 (range = 2f)
            var lantern = GetComponent<PlayerLantern>();
            if (lantern != null)
            {
                lantern.range = Mathf.Max(2f, lantern.range);
                lantern.enabled = true;
            }

            // 3. Configurar la cámara centrada y con los límites exactos de los bordes del mapa
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isLevel3 = activeScene.IndexOf("Nivel3", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            activeScene.IndexOf("Level3", System.StringComparison.OrdinalIgnoreCase) >= 0;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(transform.position.x, transform.position.y, mainCam.transform.position.z);
                var camFollow = mainCam.GetComponent<CameraFollow>();
                if (camFollow != null)
                {
                    camFollow.target = transform;
                    camFollow.lerpSpeed = Mathf.Max(camFollow.lerpSpeed, 5f);
                }
                var camBounds = mainCam.GetComponent<CameraBounds2D>();
                if (camBounds != null)
                {
                    camBounds.minBounds = isLevel3 ? new Vector2(-22f, -17f) : new Vector2(-22f, -13f);
                    camBounds.maxBounds = isLevel3 ? new Vector2(26f, 13f) : new Vector2(22f, 13f);
                }
            }

            // 4. Quitar SortingGroup y WorldDepth2D de todos los objetos de escenario para que el ordenamiento por pivote Y en Layer 1 (order = 2) funcione igual que en Nivel 1
            foreach (var wd in FindObjectsOfType<WorldDepth2D>(true))
            {
                if (wd != null) Destroy(wd);
            }

            // 5. Asegurar colisión y ordenamiento correcto en altares y props (bancas, tumbas, árboles)
            foreach (var t in FindObjectsOfType<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name;
                if (n.StartsWith("Altar_Encounter_"))
                {
                    var sg = t.GetComponent<SortingGroup>();
                    if (sg != null) Destroy(sg);

                    foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        sr.sortingLayerName = "Layer 1";
                        sr.spriteSortPoint = SpriteSortPoint.Pivot;
                        sr.sortingOrder = sr.name.Contains("Shadow") ? 1 : 2;
                    }

                    var solid = t.GetComponent<CircleCollider2D>();
                    if (solid == null) solid = t.gameObject.AddComponent<CircleCollider2D>();
                    solid.enabled = true;
                    solid.isTrigger = false;
                    solid.offset = new Vector2(0f, 0.70f);
                    solid.radius = 1.40f;

                    var box = t.GetComponent<BoxCollider2D>();
                    if (box != null)
                    {
                        box.enabled = true;
                        box.isTrigger = false;
                    }
                }
                else if (n.StartsWith("PF Props") || n.StartsWith("PF Village") || n.Contains("Bench") ||
                         n.Contains("Gravestone") || n.Contains("Cross") || n.Contains("Well") ||
                         n.Contains("Tree") || n.Contains("Statue") || n.Contains("Coffin") ||
                         n.Contains("Bush"))
                {
                    var sg = t.GetComponent<SortingGroup>();
                    if (sg != null) Destroy(sg);

                    Transform lower = t.Find("Lower");
                    if (lower != null)
                    {
                        var lowerSr = lower.GetComponent<SpriteRenderer>();
                        if (lowerSr != null)
                        {
                            lowerSr.sortingLayerName = "Layer 1";
                            lowerSr.sortingOrder = 2;
                            lowerSr.spriteSortPoint = SpriteSortPoint.Pivot;
                        }
                    }
                    Transform upper = t.Find("Upper");
                    if (upper != null)
                    {
                        var upperSr = upper.GetComponent<SpriteRenderer>();
                        if (upperSr != null)
                        {
                            upperSr.sortingLayerName = "Layer 2";
                            upperSr.sortingOrder = 2;
                            upperSr.spriteSortPoint = SpriteSortPoint.Pivot;
                        }
                    }

                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sortingLayerName = "Layer 1";
                        sr.sortingOrder = 2;
                        sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    }
                    Transform shadow = t.Find("Shadow");
                    if (shadow != null)
                    {
                        var shadowSr = shadow.GetComponent<SpriteRenderer>();
                        if (shadowSr != null)
                        {
                            shadowSr.sortingLayerName = "Layer 1";
                            shadowSr.sortingOrder = 1;
                            shadowSr.spriteSortPoint = SpriteSortPoint.Pivot;
                        }
                    }
                    if (n.Contains("Bench 01 S") || n.Contains("Stone Bench 01 S"))
                    {
                        var benchBox = t.GetComponent<BoxCollider2D>();
                        if (benchBox != null)
                        {
                            benchBox.offset = new Vector2(0f, 0.50f);
                            benchBox.size = new Vector2(1.70f, 0.95f);
                            benchBox.isTrigger = false;
                        }
                    }
                    foreach (var col in t.GetComponents<Collider2D>())
                    {
                        col.enabled = true;
                        if (!col.gameObject.name.Contains("Trigger"))
                            col.isTrigger = false;
                    }
                }
            }

            // 6. Asegurar que los duendes activos tengan EnemyHealth, SortingGroup y barra de vida
            foreach (var goblin in FindObjectsOfType<GoblinAI>(false))
            {
                if (goblin == null) continue;
                goblin.enablePatrol = true;
                var sr = goblin.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingLayerName = "Layer 1";
                    sr.sortingOrder = 2;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                }
                if (goblin.GetComponent<EnemyHealth>() == null)
                {
                    var eh = goblin.gameObject.AddComponent<EnemyHealth>();
                    eh.ConfigureVariant(EnemyHealth.EnemyVariant.NormalGoblin);
                }
                var oldBar = goblin.GetComponent<EnemyFloatingHealthBar>();
                if (oldBar != null) Destroy(oldBar);
            }

            var awarenessUI = FindObjectOfType<GoblinAwarenessUI>(true);
            if (awarenessUI != null)
            {
                awarenessUI.gameObject.SetActive(true);
                if (mainCam != null) awarenessUI.worldCamera = mainCam;
            }

            var healthUI = FindObjectOfType<GoblinHealthUI>(true);
            if (healthUI != null)
            {
                healthUI.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || (health != null && health.currentHealth <= 0))
            {
                movement = Vector2.zero;
                if (animator != null) animator.SetBool("IsMoving", false);
                return;
            }

            Vector2 dir = Vector2.zero;

            // Soporte de Teclas A/D y Flechas
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                dir.x = -1;
                if (animator != null) animator.SetInteger("Direction", 3);
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                dir.x = 1;
                if (animator != null) animator.SetInteger("Direction", 2);
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }

            // Soporte de Teclas W/S y Flechas
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                dir.y = 1;
                if (animator != null) animator.SetInteger("Direction", 1);
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                dir.y = -1;
                if (animator != null) animator.SetInteger("Direction", 0);
            }

            // Soporte GameControls (mandos / input system)
            if (dir == Vector2.zero && GameControls.Move != Vector2.zero)
            {
                dir = GameControls.Move;
                if (dir.x < -0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 3);
                    if (spriteRenderer != null) spriteRenderer.flipX = true;
                }
                else if (dir.x > 0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 2);
                    if (spriteRenderer != null) spriteRenderer.flipX = false;
                }
                else if (dir.y > 0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 1);
                }
                else if (dir.y < -0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 0);
                }
            }

            dir = Vector2.ClampMagnitude(dir, 1f);
            if (animator != null) animator.SetBool("IsMoving", dir.sqrMagnitude > 0.01f);

            movement = dir;
        }

        private void FixedUpdate()
        {
            if (body != null)
            {
                body.velocity = (speed > 0.1f ? speed : 3.0f) * movement;
            }
        }
    }
}
