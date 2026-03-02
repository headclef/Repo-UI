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

    [HarmonyPatch(typeof(RoundDirector), "Update")]
    [HarmonyPostfix]
    private static void RoundDirector_Update_Postfix()
    {
        if (!SemiFunc.RunIsLevel()) return;

        try
        {
            // Recreate every time it's null — the game destroys unknown
            // HUD children during level transitions.
            if (_overlayRoot == null)
            {
                _overlayRoot = null;  // clear stale C# ref
                _overlayText = null;
                CreateOverlay();
                if (_overlayRoot == null) return;   // creation failed
            }

            // Input: Tab hold OR map toggled (sticky)
            bool mapToggled = false;
            if (MapToolController.instance != null)
                mapToggled = Traverse.Create(MapToolController.instance)
                                     .Field("mapToggled").GetValue<bool>();

            bool shouldShow = SemiFunc.InputHold((InputKey)8) || mapToggled;

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
    }

    private static void CreateOverlay()
    {
        var gameHud = GameObject.Find("Game Hud");
        if (gameHud == null)
        {
            HeadclefUI.Logger.LogWarning("Tab overlay: 'Game Hud' not found.");
            return;
        }

        var taxHaul = GameObject.Find("Tax Haul");
        if (taxHaul == null)
        {
            HeadclefUI.Logger.LogWarning("Tab overlay: 'Tax Haul' not found.");
            return;
        }

        // Get font exactly how the working MapValueTracker mod does it
        TMP_FontAsset font = taxHaul.GetComponent<TMP_Text>().font;

        HeadclefUI.Logger.LogInfo($"CreateOverlay: gameHud='{gameHud.name}', font='{font?.name}'");

        // ── Create exactly like the working MapValueTracker reference mod ──
        // Single GameObject, TextMeshProUGUI directly on it, NO Image component
        _overlayRoot = new GameObject();
        _overlayRoot.SetActive(false);                         // start hidden
        _overlayRoot.name = "Headclef Tab Overlay";

        // Add TMP text directly on the root (same pattern as reference mod)
        _overlayRoot.AddComponent<TextMeshProUGUI>();
        _overlayText = _overlayRoot.GetComponent<TextMeshProUGUI>();
        _overlayText.font = font;
        _overlayText.color = new Color(0.79f, 0.91f, 0.90f, 1f);
        _overlayText.fontSize = 18f;
        _overlayText.enableWordWrapping = true;
        _overlayText.alignment = TextAlignmentOptions.TopRight;
        _overlayText.horizontalAlignment = HorizontalAlignmentOptions.Right;
        _overlayText.verticalAlignment = VerticalAlignmentOptions.Top;

        // Parent to Game Hud (same as reference mod)
        _overlayRoot.transform.SetParent(gameHud.transform, false);

        // Set anchoring exactly like the working reference mod:
        // anchor to full width at bottom, then use offsets to position
        var rect = _overlayRoot.GetComponent<RectTransform>();
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(1f, -1f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(0f, 0f);
        // Position in upper-right area of the HUD (offset from bottom)
        rect.offsetMax = new Vector2(0f, 350f);
        rect.offsetMin = new Vector2(400f, 150f);

        HeadclefUI.Logger.LogInfo($"Overlay created. activeInHierarchy={_overlayRoot.activeInHierarchy}, parent='{_overlayRoot.transform.parent?.name}'");
        HeadclefUI.Logger.LogInfo($"isDestroyed check: rootNull={_overlayRoot == null}, refNull={ReferenceEquals(_overlayRoot, null)}");
    }

    private static void UpdateContent()
    {
        if (_overlayText == null) return;

        var sb = new System.Text.StringBuilder();
        
        // ── Empty line to push one line below ──
        sb.AppendLine();

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

        // ── Map Value ──
        MapValueTracker.UpdateIfNeeded();
        float mapValue = MapValueTracker.TotalValue;
        sb.AppendLine($"<color=#aaaaaa>Map Value:</color> <color=#55ff55>${mapValue:N0}</color>");

        // ── Haul Progress ──
        if (RoundDirector.instance != null)
        {
            int haulGoal = Traverse.Create(RoundDirector.instance)
                .Field("extractionHaulGoal").GetValue<int>();
            int currentHaul = RoundDirector.instance.currentHaul;

            if (haulGoal > 0)
            {
                float pct = (float)currentHaul / haulGoal * 100f;
                string pctColor = pct >= 100f ? "#55ff55" : pct >= 50f ? "#ffff55" : "#ff5555";
                sb.AppendLine($"<color=#aaaaaa>Haul:</color> <color={pctColor}>${currentHaul:N0} / ${haulGoal:N0}</color>");
            }
        }

        // ── Weapon / Combat Info ──
        sb.AppendLine("<color=#ff9600><b>Combat</b></color>");
        AppendCombatInfo(sb);

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
    }

    private static void AppendCombatInfo(System.Text.StringBuilder sb)
    {
        // Try to find a gun the player is currently holding
        var gun = GetHeldGun();

        if (gun != null)
        {
            // ── Holding a gun ──
            int currentBars = gun.itemBattery != null ? gun.itemBattery.currentBars : 0;
            int totalBars   = gun.itemBattery != null ? gun.itemBattery.batteryBars : 0;
            int damage       = gun.hurtCollider != null ? gun.hurtCollider.enemyDamage : 0;

            string ammoColor = currentBars > 0 ? "#55ff55" : "#ff5555";
            sb.AppendLine($"  <color=#aaaaaa>Weapon:</color> <color=#cccccc>{gun.gameObject.name}</color>");
            sb.AppendLine($"  <color=#aaaaaa>Ammo:</color> <color={ammoColor}>{currentBars} / {totalBars}</color>");
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
    /// Try to find an ItemGun the local player is currently holding.
    /// Checks both physics-grab (pre-equip) and inventory (equipped) states.
    /// </summary>
    private static ItemGun? GetHeldGun()
    {
        try
        {
            // 1. Check if we're physics-grabbing a gun (before it's equipped)
            if (PhysGrabber.instance != null && PhysGrabber.instance.grabbed)
            {
                var grabbedObj = PhysGrabber.instance.grabbedPhysGrabObject;
                if (grabbedObj != null)
                {
                    var gun = grabbedObj.GetComponent<ItemGun>();
                    if (gun != null) return gun;
                }
            }

            // 2. Check inventory slots for an equipped gun
            if (Inventory.instance != null)
            {
                foreach (var spot in Inventory.instance.inventorySpots)
                {
                    if (spot == null) continue;

                    var equippable = spot.CurrentItem;
                    if (equippable == null) continue;

                    var gun = equippable.GetComponent<ItemGun>();
                    if (gun != null) return gun;
                }
            }

            return null;
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

            // Access tumble directly via the public field (not GetComponentInChildren
            // which misses inactive children)
            var tumble = PlayerAvatar.instance.tumble;
            if (tumble == null) return 0;

            // The HurtCollider is a public field on PlayerTumble
            var hurtCollider = tumble.hurtCollider;
            if (hurtCollider == null) return 0;

            int baseDmg = hurtCollider.enemyDamage;
            if (baseDmg <= 0) baseDmg = 12; // fallback to known default

            // Read upgrade level from StatsManager directly (same data the
            // TumbleLaunchDamagePatch reads via Character_Stats)
            string steamId = SemiFunc.PlayerGetSteamID(PlayerAvatar.instance);
            int tumbleUpgrades = 0;
            if (StatsManager.instance != null &&
                StatsManager.instance.playerUpgradeLaunch.TryGetValue(steamId, out int lvl))
            {
                tumbleUpgrades = lvl;
            }

            // Apply the same scaling formula the TumbleLaunchDamagePatch uses
            if (tumbleUpgrades > 0 && Increase_Tumble_Damage.Increase_Tumble_Damage.EnableDamageOnEnemy.Value)
            {
                float multPerLvl = Increase_Tumble_Damage.Increase_Tumble_Damage.MultiplierPerLevel.Value;
                float maxMult = Increase_Tumble_Damage.Increase_Tumble_Damage.MaxMultiplier.Value;

                float multiplier = multPerLvl * tumbleUpgrades;
                if (maxMult > 0f)
                    multiplier = Math.Min(multiplier, maxMult);

                if (multiplier > 0f)
                    return Mathf.RoundToInt(baseDmg * multiplier);
            }

            return baseDmg;
        }
        catch
        {
            return 0;
        }
    }
}
