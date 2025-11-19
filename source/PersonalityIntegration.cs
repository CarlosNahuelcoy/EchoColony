using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

public static class PersonalityIntegration
{
    public static string GetPersonalitySummary(Pawn pawn)
{
    if (!pawn.RaceProps?.Humanlike ?? true)
    {
        Log.Message("[EchoColony] ⚠️ Pawn no humanoide ignorado para personalidad.");
        return null;
    }

    if (!ModsConfig.IsActive("hahkethomemah.simplepersonalities"))
    {
        Log.Message("[EchoColony] ❌ El mod Simple Personalities no está activo.");
        return null;
    }

    try
    {
        var enneagramComp = pawn.AllComps.FirstOrDefault(c => c.GetType().FullName == "SPM1.Comps.CompEnneagram");
        if (enneagramComp == null)
        {
            Log.Warning("[EchoColony] ❌ No se encontró CompEnneagram en el pawn.");
            return null;
        }

        var enneagramProp = enneagramComp.GetType().GetProperty("Enneagram", BindingFlags.Instance | BindingFlags.Public);
        var enneagram = enneagramProp?.GetValue(enneagramComp);
        if (enneagram == null)
        {
            Log.Warning("[EchoColony] ❌ La propiedad 'Enneagram' es null.");
            return null;
        }

        string GetLabelFromDef(object def)
        {
            if (def == null) return null;
            var labelProp = def.GetType().GetProperty("LabelCap") ?? def.GetType().GetProperty("label");
            return labelProp?.GetValue(def)?.ToString() ?? def.ToString();
        }

        string ReadDefLabel(string fieldName)
        {
            var field = enneagram.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            var def = field?.GetValue(enneagram);
            return GetLabelFromDef(def);
        }

        string rootLabel = ReadDefLabel("Root") ?? "Unknown type";
        string variantLabel = ReadDefLabel("Variant") ?? "Unknown variant";

        List<string> traits = new List<string>();
        foreach (string traitField in new[] { "MainTrait", "SecondaryTrait", "OptionalTrait" })
        {
            string traitLabel = ReadDefLabel(traitField);
            if (!string.IsNullOrWhiteSpace(traitLabel)) traits.Add(traitLabel);
        }
        string traitsList = traits.Count > 0 ? string.Join(", ", traits) : "Unknown traits";

        string drive = null;
        var rootField = enneagram.GetType().GetField("Root", BindingFlags.Public | BindingFlags.Instance);
        var rootObj = rootField?.GetValue(enneagram);
        if (rootObj != null)
        {
            var driveField = rootObj.GetType().GetField("drive", BindingFlags.Public | BindingFlags.Instance);
            var driveObj = driveField?.GetValue(rootObj);
            drive = GetLabelFromDef(driveObj);
        }

        if (drive == null) drive = "Unknown drive";

        Log.Message($"[EchoColony] 🎭 Personalidad extraída: Root: {rootLabel}, Variant: {variantLabel}, Drive: {drive}, Traits: {traitsList}");

        return $"Personality Type: {rootLabel} ({variantLabel}), Driven by: {drive}, Character Traits: {traitsList}";
    }
    catch (Exception ex)
    {
        Log.Warning("[EchoColony] ⚠️ Error accediendo a personalidad desde Enneagram: " + ex.Message);
        return null;
    }
}

}
