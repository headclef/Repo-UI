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
        int mapLevel = 0;
        if (StatsManager.instance?.runStats != null &&
            StatsManager.instance.runStats.ContainsKey("level"))
        {
            mapLevel = StatsManager.instance.runStats["level"] + 1;
        }
        
        int impLevel = Improve.SaveData.CurrentLevel();
        int impPoints = Improve.SaveData.AvailablePoints();
        
        sb.AppendLine($"<color=#ff9600><size=22><b>Map Level {mapLevel}</b></size></color>");
        sb.AppendLine($"<color=#00ff99><size=22><b>Improve Level {impLevel}</b></size></color>");
        sb.AppendLine($"<color=#cccccc>Available Points:</color> <color=#ffffff>{impPoints}</color>");
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

        // ── Weapon / Combat Info ──
        sb.AppendLine("<color=#ff9600><b>Combat</b></color>");
        AppendCombatInfo(sb);

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

    private static void AppendCombatInfo(System.Text.StringBuilder sb)
    {
        // Try to find a gun the player is currently holding
        var gun = GetHeldGun();

        if (gun != null)
        {
            // ── Holding a gun ──
            int batteryCurrent = Traverse.Create(gun).Field("batteryCurrent").GetValue<int>();
            int batteryMax = Traverse.Create(gun).Field("batteryMax").GetValue<int>();
            int damage = Traverse.Create(gun).Field("gunDamage").GetValue<int>();

            string ammoColor = batteryCurrent > 0 ? "#55ff55" : "#ff5555";
            sb.AppendLine($"  <color=#aaaaaa>Weapon:</color> <color=#cccccc>{gun.gameObject.name}</color>");
            sb.AppendLine($"  <color=#aaaaaa>Ammo:</color> <color={ammoColor}>{batteryCurrent} / {batteryMax}</color>");
            sb.AppendLine($"  <color=#aaaaaa>Damage:</color> <color=#ffaa55>{damage}</color>");
        }
        else
        {
            // ── Not holding a gun — show tumble launch damage ──
            int tumbleDamage = GetTumbleLaunchDamage();
            if (tumbleDamage > 0)
            {
                sb.AppendLine($"  <color=#aaaaaa>Tumble Launch:</color> <color=#ffaa55>{tumbleDamage} dmg</color>");
            }
            else
            {
                sb.AppendLine($"  <color=#666666>No weapon held</color>");
            }
        }
    }

    /// <summary>
    /// Try to find an ItemGun component on the object the player is currently grabbing.
    /// </summary>
    private static ItemGun? GetHeldGun()
    {
        try
        {
            if (PlayerController.instance == null) return null;

            var physGrabber = PlayerController.instance.GetComponentInChildren<PhysGrabber>();
            if (physGrabber == null) return null;

            var grabbed = Traverse.Create(physGrabber).Field("grabbedPhysGrabObject").GetValue<PhysGrabObject>();
            if (grabbed == null) return null;

            return grabbed.GetComponent<ItemGun>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current tumble launch enemy damage.
    /// This reads the live HurtCollider.enemyDamage value, which is the base,
    /// and dynamically calculates the Increase Tumble Damage mod scaling.
    /// </summary>
    private static int GetTumbleLaunchDamage()
    {
        try
        {
            if (PlayerAvatar.instance == null) return 0;

            var tumble = PlayerAvatar.instance.GetComponentInChildren<PlayerTumble>();
            if (tumble == null) return 0;

            // The HurtCollider on the tumble object has the base enemyDamage field (usually 12)
            var hurtCollider = tumble.GetComponentInChildren<HurtCollider>();
            if (hurtCollider == null) return 0;

            int baseDmg = hurtCollider.enemyDamage;
            
            // Apply Tumble scaling from Improve Mod
            try
            {
                int upgrades = Improve.SaveData.AllocTumbleLaunch.Value;
                if (upgrades > 0)
                {
                    Type tumbleType = typeof(Increase_Tumble_Damage.Increase_Tumble_Damage);
                    bool enabled = Traverse.Create(tumbleType).Field("EnableDamageOnEnemy").GetValue<BepInEx.Configuration.ConfigEntry<bool>>()?.Value ?? false;
                    
                    if (enabled)
                    {
                        float multPerLvl = Traverse.Create(tumbleType).Field("MultiplierPerLevel").GetValue<BepInEx.Configuration.ConfigEntry<float>>()?.Value ?? 0f;
                        float maxMult = Traverse.Create(tumbleType).Field("MaxMultiplier").GetValue<BepInEx.Configuration.ConfigEntry<float>>()?.Value ?? 0f;
                        
                        float multiplier = multPerLvl * upgrades;
                        if (maxMult > 0f)
                            multiplier = Math.Min(multiplier, maxMult);
                            
                        if (multiplier > 0f)
                            return Mathf.RoundToInt(baseDmg * multiplier);
                    }
                }
            }
            catch
            {
                // Fallback if the mod isn't loaded correctly
            }

            return baseDmg;
        }
        catch
        {
            return 0;
        }
    }
}
