using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeadclefUI;

/// <summary>
/// Creates and manages the Tab overlay — a unified info panel shown
/// while holding Tab, positioned at center-right of the screen.
/// Shows: Level number, map value, haul progress, and player list with status.
/// </summary>
[HarmonyPatch]
public static class TabOverlay
{
    private static GameObject? _overlayRoot;
    private static TextMeshProUGUI? _overlayText;
    private static bool _initialized;

    [HarmonyPatch(typeof(RoundDirector), "Update")]
    [HarmonyPostfix]
    private static void RoundDirector_Update_Postfix()
    {
        if (!SemiFunc.RunIsLevel()) return;

        try
        {
            if (!_initialized)
            {
                CreateOverlay();
                _initialized = true;
            }

            if (_overlayRoot == null || _overlayText == null) return;

            // Show only while Tab is held (InputKey 8 = Tab/Map key)
            bool tabHeld = SemiFunc.InputHold((InputKey)8);
            bool mapToggled = false;

            if (MapToolController.instance != null)
            {
                mapToggled = Traverse.Create(MapToolController.instance)
                    .Field("mapToggled").GetValue<bool>();
            }

            bool shouldShow = tabHeld || mapToggled;
            _overlayRoot.SetActive(shouldShow);

            if (shouldShow)
            {
                UpdateContent();
            }
        }
        catch (Exception ex)
        {
            HeadclefUI.Logger.LogError($"TabOverlay exception: {ex.Message}");
        }
    }

    /// <summary>Reset overlay when leaving a level.</summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.OnSceneSwitch))]
    [HarmonyPrefix]
    private static void OnSceneSwitch_Prefix()
    {
        if (_overlayRoot != null)
        {
            UnityEngine.Object.Destroy(_overlayRoot);
            _overlayRoot = null;
            _overlayText = null;
        }
        _initialized = false;
    }

    private static void CreateOverlay()
    {
        var gameHud = GameObject.Find("Game Hud");
        if (gameHud == null) return;

        // Find a TMP font from existing UI
        var taxHaul = GameObject.Find("Tax Haul");
        TMP_FontAsset? font = null;
        if (taxHaul != null)
        {
            var tmp = taxHaul.GetComponent<TMP_Text>();
            if (tmp != null) font = tmp.font;
        }

        // Create container
        _overlayRoot = new GameObject("Headclef Tab Overlay");
        _overlayRoot.SetActive(false);
        _overlayRoot.transform.SetParent(gameHud.transform, false);

        // Add background panel for readability
        var bg = _overlayRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // Position: center-right
        var rectBg = _overlayRoot.GetComponent<RectTransform>();
        rectBg.anchorMin = new Vector2(1f, 0.5f);
        rectBg.anchorMax = new Vector2(1f, 0.5f);
        rectBg.pivot = new Vector2(1f, 0.5f);
        rectBg.anchoredPosition = new Vector2(-20f, 0f);
        rectBg.sizeDelta = new Vector2(280f, 300f);

        // Add text element
        var textObj = new GameObject("Overlay Text");
        textObj.transform.SetParent(_overlayRoot.transform, false);

        _overlayText = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) _overlayText.font = font;
        _overlayText.fontSize = 18f;
        _overlayText.color = new Color(0.79f, 0.91f, 0.90f, 1f);
        _overlayText.enableWordWrapping = true;
        _overlayText.alignment = TextAlignmentOptions.TopLeft;
        _overlayText.overflowMode = TextOverflowModes.Overflow;

        var rectText = textObj.GetComponent<RectTransform>();
        rectText.anchorMin = Vector2.zero;
        rectText.anchorMax = Vector2.one;
        rectText.offsetMin = new Vector2(12f, 10f);
        rectText.offsetMax = new Vector2(-12f, -10f);

        HeadclefUI.Logger.LogDebug("Tab overlay created.");
    }

    private static void UpdateContent()
    {
        if (_overlayText == null) return;

        var sb = new System.Text.StringBuilder();

        // ── Level ──
        int level = 0;
        if (StatsManager.instance?.runStats != null &&
            StatsManager.instance.runStats.ContainsKey("level"))
        {
            level = StatsManager.instance.runStats["level"] + 1;
        }
        sb.AppendLine($"<color=#ff9600><size=22><b>Level {level}</b></size></color>");
        sb.AppendLine();

        // ── Map Value ──
        float mapValue = MapValueTracker.TotalValue;
        sb.AppendLine($"<color=#aaaaaa>Map Value:</color> <color=#55ff55>${mapValue:N0}</color>");

        // ── Haul Progress ──
        if (RoundDirector.instance != null)
        {
            int haulGoal = Traverse.Create(RoundDirector.instance)
                .Field("extractionHaulGoal").GetValue<int>();
            int currentHaul = StatsManager.instance?.GetRunStatTotalHaul() ?? 0;

            if (haulGoal > 0)
            {
                float pct = (float)currentHaul / haulGoal * 100f;
                string pctColor = pct >= 100f ? "#55ff55" : pct >= 50f ? "#ffff55" : "#ff5555";
                sb.AppendLine($"<color=#aaaaaa>Haul:</color> <color={pctColor}>${currentHaul:N0} / ${haulGoal:N0}</color>");
            }
        }

        sb.AppendLine();

        // ── Player List ──
        sb.AppendLine("<color=#ff9600><b>Players</b></color>");

        var players = SemiFunc.PlayerGetAll();
        if (players != null)
        {
            foreach (var player in players)
            {
                if (player == null) continue;

                string name = player.playerName ?? "Unknown";
                string status;
                string statusColor;

                if (player.deadSet)
                {
                    status = "Dead";
                    statusColor = "#ff5555";
                }
                else
                {
                    status = "Alive";
                    statusColor = "#55ff55";
                }

                sb.AppendLine($"  <color=#cccccc>{name}</color> — <color={statusColor}>{status}</color>");
            }
        }

        _overlayText.text = sb.ToString();

        // Resize background to fit content
        if (_overlayRoot != null)
        {
            var preferredHeight = _overlayText.preferredHeight + 24f;
            var preferredWidth = Math.Max(_overlayText.preferredWidth + 28f, 240f);
            var rectBg = _overlayRoot.GetComponent<RectTransform>();
            rectBg.sizeDelta = new Vector2(Math.Min(preferredWidth, 320f), Math.Max(preferredHeight, 120f));
        }
    }
}
