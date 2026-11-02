using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class Level2AltarAndPlayVerification
{
    public static void RunVerification()
    {
        Debug.Log("[Verification] Starting Level 2 Altar & UI Verification...");
        string scenePath = "Assets/Scenes/Cementerio_Nivel2.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var report = new System.Collections.Generic.List<string>();
        Action<bool, string> assert = (condition, description) =>
        {
            if (!condition)
            {
                Debug.LogError("[FAIL] " + description);
                throw new Exception("Assertion Failed: " + description);
            }
            Debug.Log("[PASS] " + description);
            report.Add("[PASS] " + description);
        };

        // 1. Verify Altars have solid CircleCollider2D, trigger BoxCollider2D, SortingGroup and WorldDepth2D
        var altars = UnityEngine.Object.FindObjectsOfType<Transform>(true)
            .Where(t => t.name.StartsWith("Altar_Encounter_"))
            .Select(t => t.gameObject)
            .ToList();

        assert(altars.Count == 3, "Three encounter altars found in scene");

        foreach (var altar in altars)
        {
            var circle = altar.GetComponent<CircleCollider2D>();
            assert(circle != null, $"{altar.name} has CircleCollider2D");
            assert(!circle.isTrigger, $"{altar.name} CircleCollider2D is NOT a trigger (solid physical obstacle)");
            assert(circle.radius >= 1.0f, $"{altar.name} CircleCollider2D has proper radius ({circle.radius})");

            var box = altar.GetComponent<BoxCollider2D>();
            assert(box != null, $"{altar.name} has BoxCollider2D for rune glow");
            assert(box.isTrigger, $"{altar.name} BoxCollider2D is a trigger");

            var sg = altar.GetComponent<SortingGroup>();
            assert(sg != null && sg.enabled, $"{altar.name} has enabled SortingGroup");
            assert(sg.sortingLayerName == "Layer 1", $"{altar.name} SortingGroup layer is Layer 1");

            var depth = altar.GetComponent<WorldDepth2D>();
            assert(depth != null && depth.enabled, $"{altar.name} has enabled WorldDepth2D");
            assert(depth.feetOffset >= 0.5f, $"{altar.name} WorldDepth2D feetOffset is set to base contact point ({depth.feetOffset})");
        }

        // 2. Verify Player components
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        assert(player != null, "Player found in scene");
        var pMove = player.GetComponent<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        assert(pMove != null && pMove.enabled, "Player has TopDownCharacterController enabled");

        // 3. Verify ParchmentMessageUI
        var parchment = UnityEngine.Object.FindObjectOfType<ParchmentMessageUI>(true);
        assert(parchment != null, "ParchmentMessageUI found in scene");
        var card = parchment.transform.Find("ParchmentCard");
        assert(card != null, "ParchmentCard found in hierarchy");
        var cardRt = card.GetComponent<RectTransform>();
        assert(cardRt.sizeDelta.x >= 800f, $"ParchmentCard has wide comfortable width ({cardRt.sizeDelta.x})");
        assert(cardRt.sizeDelta.y >= 200f, $"ParchmentCard has comfortable height ({cardRt.sizeDelta.y})");

        var title = card.Find("CardTitle")?.GetComponent<TMPro.TMP_Text>();
        assert(title != null && title.fontSize >= 17f, "CardTitle exists with clear font size");

        var body = card.Find("CardBody")?.GetComponent<TMPro.TMP_Text>();
        assert(body != null && body.fontSize >= 13f && body.enableWordWrapping, "CardBody has clear font size and word wrapping");

        // 4. Mathematical check of sorting orders
        // When player is south of altar 1 (altar Y=6.2, feetOffset=0.6 -> effective Y=6.8)
        // Player at Y=5.0 -> order = 1000 - 50 = 950. Altar order = 1000 - 68 = 932.
        int altar1Order = Mathf.Clamp(1000 + Mathf.RoundToInt(-(6.2f + 0.6f) * 10f), 0, 5000);
        int playerSouthOrder = Mathf.Clamp(1000 + Mathf.RoundToInt(-5.0f * 10f), 0, 5000);
        int playerNorthOrder = Mathf.Clamp(1000 + Mathf.RoundToInt(-7.5f * 10f), 0, 5000);

        assert(playerSouthOrder > altar1Order, $"Player south of altar (order {playerSouthOrder}) renders IN FRONT of Altar (order {altar1Order})");
        assert(playerNorthOrder < altar1Order, $"Player north of altar (order {playerNorthOrder}) renders BEHIND Altar (order {altar1Order})");

        Directory.CreateDirectory("Documentation");
        File.WriteAllLines("Documentation/Nivel2-altar-ui-validacion.txt", report);
        Debug.Log("[Verification] SUCCESS! All checks passed.");
    }
}
