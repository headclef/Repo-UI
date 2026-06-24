using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeadclefUI;

/// <summary>
/// Creates and manages the Tab overlay, shown while holding Tab. Two right-side
/// panels: a top-right INFO panel (map value, combat/tumble) sitting just below the
/// game's HUD, and a bottom-right PLAYER panel that grows UPWARD as players join.
/// </summary>
[HarmonyPatch]
public static class TabOverlay
{
    private static GameObject? _overlayRoot;
    private static TextMeshProUGUI? _infoText;   // top-right panel: map value, combat
    private static TextMeshProUGUI? _playerText; // bottom-right panel: player list, grows up

    // ── Soft-dependency presence flags (checked once) ──
    // UI references Improve / Character Stats / Increase Tumble Damage as SOFT
    // dependencies. Touching one of their types inside a method forces the runtime
    // to resolve that assembly when the method is JIT-compiled — which throws (and a
    // try/catch inside the SAME method does not help) when the mod isn't installed.
    // So every block that touches those types lives in its own method that is only
    // CALLED when the corresponding mod is present.
    private static bool _depsChecked;
    private static bool _improveLoaded;
    private static bool _charStatsLoaded;
    private static bool _tumbleDamageLoaded;

    private static void EnsureDepsChecked()
    {
        if (_depsChecked) return;
        _depsChecked = true;

        var plugins = Chainloader.PluginInfos;
        _improveLoaded = plugins.ContainsKey("headclef.Improve");
        _charStatsLoaded = plugins.ContainsKey("headclef.CharacterStats");
        _tumbleDamageLoaded = plugins.ContainsKey("headclef.IncreaseTumbleDamage");
    }

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
                _infoText = null;
                _playerText = null;
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
            _infoText = null;
            _playerText = null;
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
        TMP_FontAsset? font = taxHaul.GetComponent<TMP_Text>()?.font;
        if (font is null)
        {
            HeadclefUI.Logger.LogWarning("Tab overlay: HUD font not found.");
            return;
        }

        // HeadclefUI.Logger.LogInfo($"CreateOverlay: gameHud='{gameHud.name}', font='{font?.name}'");

        // Container that fills the HUD; the two panels anchor to its corners.
        _overlayRoot = new GameObject("Headclef Tab Overlay", typeof(RectTransform));
        _overlayRoot.SetActive(false);                         // start hidden
        _overlayRoot.transform.SetParent(gameHud.transform, false);

        var rootRect = (RectTransform)_overlayRoot.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // ── Top-right INFO panel ── sits just below the game's top HUD and grows
        // downward. Holds the static world/combat info (Map Value, Combat, Tumble).
        _infoText = CreateText("Info", font, _overlayRoot.transform,
            anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
            anchoredPos: new Vector2(-25f, -80f),
            alignment: TextAlignmentOptions.TopRight);

        // ── Bottom-right PLAYER panel ── bottom-aligned so the list grows UPWARD as
        // players join, into the empty middle — never overlapping the info panel or
        // the game's UI for realistic lobby sizes.
        _playerText = CreateText("Players", font, _overlayRoot.transform,
            anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-25f, 40f),
            alignment: TextAlignmentOptions.BottomRight);

        // HeadclefUI.Logger.LogInfo($"Overlay created. activeInHierarchy={_overlayRoot.activeInHierarchy}, parent='{_overlayRoot.transform.parent?.name}'");
        // HeadclefUI.Logger.LogInfo($"isDestroyed check: rootNull={_overlayRoot == null}, refNull={ReferenceEquals(_overlayRoot, null)}");
    }

    /// <summary>Creates a right-aligned TMP panel under <paramref name="parent"/>.</summary>
    private static TextMeshProUGUI CreateText(string name, TMP_FontAsset? font, Transform parent,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.color = new Color(0.79f, 0.91f, 0.90f, 1f);
        tmp.fontSize = 15f;
        tmp.enableWordWrapping = true;
        tmp.alignment = alignment;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(420f, 600f);
        rect.anchoredPosition = anchoredPos;

        return tmp;
    }

    private static void UpdateContent()
    {
        if (_infoText == null || _playerText == null) return;

        EnsureDepsChecked();

        // ── Top panel: static world / combat info (just below the game's top HUD) ──
        var info = new System.Text.StringBuilder();

        // Improve integration (soft dependency) — only touch Improve types if loaded
        if (_improveLoaded)
            AppendImproveInfo(info);

        MapValueTracker.UpdateIfNeeded();
        float mapValue = MapValueTracker.TotalValue;
        info.AppendLine($"<color=#aaaaaa>Map Value:</color> <color=#55ff55>${mapValue:N0}</color>");

        AppendCombatInfo(info);

        _infoText.text = info.ToString().TrimEnd();

        // ── Bottom panel: player list (bottom-aligned, grows UPWARD) ──
        var roster = new System.Text.StringBuilder();
        roster.AppendLine("<color=#ff9600><b>Players</b></color>");

        var players = SemiFunc.PlayerGetAll();
        if (players != null)
        {
            foreach (var player in players)
            {
                if (player == null) continue;

                string name = player.playerName ?? "Unknown";
                string status = player.deadSet ? "Dead" : "Alive";
                string statusColor = player.deadSet ? "#ff5555" : "#55ff55";

                roster.AppendLine($"  <color=#cccccc>{name}</color> — <color={statusColor}>{status}</color>");
            }
        }

        _playerText.text = roster.ToString().TrimEnd();
    }

    /// <summary>
    /// Appends Improve level / available points. Isolated so the Improve assembly
    /// is only resolved when the Improve mod is actually loaded.
    /// </summary>
    private static void AppendImproveInfo(System.Text.StringBuilder sb)
    {
        try
        {
            int impLevel = Improve.SaveData.CurrentLevel();
            sb.AppendLine($"<color=#00ff99>Improve Level:</color> <color=#ffffff>{impLevel}</color>");
        }
        catch (Exception ex)
        {
            HeadclefUI.Logger.LogWarning($"Improve info unavailable: {ex.Message}");
        }
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
            int damage      = GetGunDamage(gun);

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
    /// Try to find an ItemGun the local player is currently holding in hand.
    /// In REPO, "Equipped" means stored in inventory — when a gun is taken OUT
    /// and held in hand, it's in ItemState.Idle with PhysGrabObject.grabbedLocal == true.
    /// </summary>
    private static ItemGun? _cachedFallbackGun;
    private static float _lastGunScanTime;
    private const float GunScanInterval = 0.25f;
    // private static float _lastTumbleDiag;  // TEMP diagnostic throttle (disabled — fix confirmed)

    private static ItemGun? GetHeldGun()
    {
        try
        {
            // Fast path: check what the local PhysGrabber is holding (cheap — every frame)
            var grabber = PhysGrabber.instance;
            if (grabber != null && grabber.grabbed && grabber.grabbedPhysGrabObject != null)
            {
                var obj = grabber.grabbedPhysGrabObject;
                // ItemGun may be on the same object or a child
                var gun = obj.GetComponent<ItemGun>()
                       ?? obj.GetComponentInChildren<ItemGun>();
                if (gun != null) return gun;
            }

            // Expensive fallback: scan all guns in scene — throttled to limit per-frame cost
            if (Time.time - _lastGunScanTime < GunScanInterval)
                return _cachedFallbackGun;
            _lastGunScanTime = Time.time;

            foreach (var gun in UnityEngine.Object.FindObjectsOfType<ItemGun>())
            {
                var physObj = gun.GetComponent<PhysGrabObject>()
                           ?? gun.GetComponentInParent<PhysGrabObject>();
                if (physObj != null && physObj.grabbedLocal)
                {
                    _cachedFallbackGun = gun;
                    return gun;
                }
            }

            _cachedFallbackGun = null;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read the actual damage for a gun.  The real value lives on the
    /// bullet prefab (ItemGunBullet.hurtCollider.enemyDamage) or, for
    /// laser guns, on SemiLaser.hurtCollider.enemyDamage.
    /// gun.hurtCollider.enemyDamage is only for physical-throw impact (usually 0).
    /// </summary>
    private static int GetGunDamage(ItemGun gun)
    {
        try
        {
            // Bullet-based guns
            if (gun.bulletPrefab != null)
            {
                var bullet = gun.bulletPrefab.GetComponent<ItemGunBullet>();
                if (bullet != null && bullet.hasHurtCollider && bullet.hurtCollider != null)
                    return bullet.hurtCollider.enemyDamage;
            }

            // Laser-based guns
            var laser = gun.GetComponent<ItemGunLaser>()
                     ?? gun.GetComponentInChildren<ItemGunLaser>();
            if (laser != null && laser.semiLaser != null && laser.semiLaser.hurtCollider != null)
                return laser.semiLaser.hurtCollider.enemyDamage;

            // Last resort: gun body's own HurtCollider
            if (gun.hurtCollider != null)
                return gun.hurtCollider.enemyDamage;
        }
        catch { }

        return 0;
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

            // Read the Launch upgrade level — prefers Character Stats (if loaded),
            // otherwise vanilla StatsManager. The mod accesses are isolated below.
            string steamId = SemiFunc.PlayerGetSteamID(PlayerAvatar.instance);
            int tumbleUpgrades = GetLaunchUpgradeLevel(steamId);

            // TEMP DIAGNOSTIC (disabled — Improve/tumble fix confirmed). Re-enable to
            // compare the level the UI uses (effLaunch) against the raw StatsManager value.
            // if (Time.time - _lastTumbleDiag > 3f)
            // {
            //     _lastTumbleDiag = Time.time;
            //     int vanillaLaunch = (StatsManager.instance != null &&
            //         StatsManager.instance.playerUpgradeLaunch.TryGetValue(steamId, out int vl)) ? vl : -1;
            //     HeadclefUI.Logger.LogInfo(
            //         $"[TumbleDiag] steamId='{steamId}' charStats={_charStatsLoaded} itd={_tumbleDamageLoaded} " +
            //         $"effLaunch={tumbleUpgrades} vanillaLaunch={vanillaLaunch} baseDmg={baseDmg}");
            // }

            // Apply Increase Tumble Damage scaling only if that mod is loaded.
            if (tumbleUpgrades > 0 && _tumbleDamageLoaded)
            {
                int scaled = GetScaledTumbleDamage(baseDmg, tumbleUpgrades);
                if (scaled > 0)
                    return scaled;
            }

            return baseDmg;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Launch upgrade level — prefers the Character Stats mod (if loaded), else
    /// vanilla StatsManager. The Character Stats access is isolated into its own
    /// method so its assembly is only resolved when that mod is present.
    /// </summary>
    private static int GetLaunchUpgradeLevel(string steamId)
    {
        if (_charStatsLoaded)
        {
            try { return GetLaunchLevelFromCharacterStats(steamId); }
            catch { /* fall through to vanilla */ }
        }

        if (StatsManager.instance != null &&
            StatsManager.instance.playerUpgradeLaunch.TryGetValue(steamId, out int lvl))
        {
            return lvl;
        }
        return 0;
    }

    private static int GetLaunchLevelFromCharacterStats(string steamId)
        => Character_Stats.Character_Stats.GetUpgradeLevel(steamId, "Launch");

    /// <summary>
    /// Applies the Increase Tumble Damage scaling formula. Isolated so its assembly
    /// is only resolved when that mod is loaded.
    /// </summary>
    private static int GetScaledTumbleDamage(int baseDmg, int upgrades)
    {
        if (!Increase_Tumble_Damage.Increase_Tumble_Damage.EnableDamageOnEnemy.Value)
            return 0;

        float multPerLvl = Increase_Tumble_Damage.Increase_Tumble_Damage.MultiplierPerLevel.Value;
        float maxMult = Increase_Tumble_Damage.Increase_Tumble_Damage.MaxMultiplier.Value;

        float multiplier = multPerLvl * upgrades;
        if (maxMult > 0f)
            multiplier = Math.Min(multiplier, maxMult);

        if (multiplier > 0f)
            return Mathf.RoundToInt(baseDmg * multiplier);
        return 0;
    }
}
