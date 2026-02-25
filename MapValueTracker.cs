using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HeadclefUI;

/// <summary>
/// Tracks the total remaining dollar value of valuable items on the current map.
/// Hooks into item spawning, value changes, breakage, destruction, and extraction.
/// </summary>
public static class MapValueTracker
{
    public static float TotalValue { get; private set; }
    public static float InitialValue { get; private set; }

    public static void Reset()
    {
        TotalValue = 0f;
        InitialValue = 0f;
    }

    public static void Recalculate(ValuableObject? ignore = null)
    {
        if (Traverse.Create(RoundDirector.instance)
            .Field("allExtractionPointsCompleted").GetValue<bool>())
            return;

        TotalValue = 0f;
        var items = Object.FindObjectsOfType<ValuableObject>().ToList();
        if (ignore != null) items.Remove(ignore);

        foreach (var item in items)
            TotalValue += item.dollarValueCurrent;
    }

    // ── Patches ──

    [HarmonyPatch(typeof(LevelGenerator), "StartRoomGeneration")]
    [HarmonyPrefix]
    private static void OnGenerationStart() => Reset();

    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    [HarmonyPrefix]
    private static void OnGenerationDone()
    {
        Recalculate();
        InitialValue = TotalValue;
    }

    [HarmonyPatch(typeof(ValuableObject), "DollarValueSetRPC")]
    [HarmonyPostfix]
    private static void OnValueSet(float value)
    {
        TotalValue += value;
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

        // Only subtract if item still has meaningful value (>15% of original)
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
