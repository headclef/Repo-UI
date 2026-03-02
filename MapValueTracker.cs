using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HeadclefUI;

/// <summary>
/// Tracks the total remaining dollar value of valuable items on the current map.
/// Uses a periodic live scan of all ValuableObject instances for reliability
/// (the game's coroutine-based value initialisation makes patch-only tracking fragile).
/// Breakage / destruction events are applied immediately so the value updates in real-time.
/// </summary>
public static class MapValueTracker
{
    public static float TotalValue { get; private set; }
    public static float InitialValue { get; private set; }

    private static float _lastRecalcTime;
    private const float RecalcInterval = 2f; // seconds between full scans

    public static void Reset()
    {
        TotalValue = 0f;
        InitialValue = 0f;
        _lastRecalcTime = 0f;
    }

    /// <summary>
    /// Full scan of every ValuableObject currently in the scene.
    /// Called periodically from the overlay update loop and after key events.
    /// </summary>
    public static void Recalculate()
    {
        if (RoundDirector.instance == null) return;

        if (Traverse.Create(RoundDirector.instance)
            .Field("allExtractionPointsCompleted").GetValue<bool>())
            return;

        TotalValue = 0f;
        var items = Object.FindObjectsOfType<ValuableObject>();

        foreach (var item in items)
        {
            if (item.dollarValueSet)
                TotalValue += item.dollarValueCurrent;
        }

        _lastRecalcTime = Time.time;
    }

    /// <summary>
    /// Called every frame from the overlay.  Only does a full scan every
    /// <see cref="RecalcInterval"/> seconds to limit perf cost.
    /// </summary>
    public static void UpdateIfNeeded()
    {
        if (Time.time - _lastRecalcTime >= RecalcInterval)
            Recalculate();
    }

    // ── Patches ──

    [HarmonyPatch(typeof(LevelGenerator), "StartRoomGeneration")]
    [HarmonyPrefix]
    private static void OnGenerationStart() => Reset();

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    [HarmonyPostfix]
    private static void OnGenerationDone()
    {
        Recalculate();
        InitialValue = TotalValue;
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
    [HarmonyPostfix]
    private static void OnBreak(float valueLost, bool _loseValue)
    {
        if (_loseValue)
            TotalValue -= valueLost;
    }

    [HarmonyPatch(typeof(PhysGrabObject), "DestroyPhysGrabObjectRPC")]
    [HarmonyPostfix]
    private static void OnDestroy(PhysGrabObject __instance)
    {
        if (!SemiFunc.RunIsLevel()) return;

        var valuable = __instance.GetComponent<ValuableObject>();
        if (valuable == null) return;

        if (valuable.dollarValueCurrent >= valuable.dollarValueOriginal * 0.15f)
            TotalValue -= valuable.dollarValueCurrent;
    }

    [HarmonyPatch(typeof(RoundDirector), "ExtractionCompleted")]
    [HarmonyPostfix]
    private static void OnExtraction()
    {
        if (SemiFunc.RunIsLevel())
            Recalculate();
    }
}
