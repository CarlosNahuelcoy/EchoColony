using System.Collections.Generic;
using System.Linq;
using EchoColony;
using RimWorld;
using UnityEngine;
using Verse;

public class ColonistMemoryViewer : Window
{
    private Pawn pawn;
    private Vector2 scrollPos;
    private Dictionary<int, string> allMemories;
    // ✅ NUEVO: Diccionario para manejar scroll individual de cada entrada
    private Dictionary<int, Vector2> entryScrollPositions = new Dictionary<int, Vector2>();
    // ✅ NUEVO: Diccionario para estados de colapso/expansión
    private Dictionary<int, bool> entryExpandedStates = new Dictionary<int, bool>();

    public ColonistMemoryViewer(Pawn pawn)
    {
        this.pawn = pawn;
        this.doCloseX = true;
        this.absorbInputAroundWindow = true;
        this.forcePause = true;
        this.closeOnClickedOutside = false;

        LoadMemories();
    }

    private bool ContainsMultipleColonistNames(string memory)
    {
        if (string.IsNullOrEmpty(memory)) return false;

        var allColonists = Find.CurrentMap?.mapPawns?.FreeColonists;
        if (allColonists == null) return false;

        int colonistNamesFound = 0;
        
        foreach (var colonist in allColonists)
        {
            if (memory.Contains(colonist.LabelShort) || 
                memory.Contains(colonist.Name?.ToStringShort ?? ""))
            {
                colonistNamesFound++;
                if (colonistNamesFound >= 2)
                    return true;
            }
        }
        
        return false;
    }

    private void LoadMemories()
    {
        var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
        allMemories = tracker?.GetAllMemories() ?? new Dictionary<int, string>();

        // ✅ INICIALIZAR estados para nuevas entradas
        foreach (var day in allMemories.Keys)
        {
            if (!entryScrollPositions.ContainsKey(day))
                entryScrollPositions[day] = Vector2.zero;
            
            if (!entryExpandedStates.ContainsKey(day))
                entryExpandedStates[day] = false; // Por defecto colapsadas
        }

        Log.Message($"[EchoColony] {"EchoColony.MemoriesLoaded".Translate(allMemories.Count, pawn.LabelShort)}");
    }

    public override Vector2 InitialSize => new Vector2(750f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        // ═══════════════ HEADER ═══════════════
        Text.Font = GameFont.Medium;
        var headerRect = new Rect(0f, 0f, inRect.width, 50f);
        
        Widgets.DrawBoxSolid(headerRect, new Color(0.15f, 0.2f, 0.3f, 0.9f));
        
        Rect portraitRect = new Rect(10f, 10f, 30f, 30f);
        GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(30f, 30f), Rot4.South, default, 1f));
        
        Widgets.Label(new Rect(50f, 12f, inRect.width - 150f, 30f), $"🧠 {"EchoColony.MemoriesOf".Translate()} {pawn.LabelCap}");
        
        Text.Font = GameFont.Small;
        GUI.color = new Color(0.8f, 0.9f, 1f);
        Widgets.Label(new Rect(inRect.width - 140f, 18f, 130f, 25f), $"📚 {allMemories.Count} {"EchoColony.MemoriesEntries".Translate()}");
        GUI.color = Color.white;

        Text.Font = GameFont.Small;
        float currentY = 60f;

        // ═══════════════ CONTENT AREA ═══════════════
        var contentRect = new Rect(0f, currentY, inRect.width, inRect.height - currentY - 50f);
        
        if (allMemories.Count == 0)
        {
            Widgets.DrawBoxSolid(contentRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(contentRect, $"📭\n\n{"EchoColony.NoMemoriesSaved".Translate()}\n\n{"EchoColony.MemoriesAutoCreated".Translate()}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        else
        {
            DrawMemories(contentRect);
        }

        // ═══════════════ FOOTER ═══════════════
        var footerRect = new Rect(0f, inRect.height - 40f, inRect.width, 35f);
        Widgets.DrawBoxSolid(footerRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // ✅ NUEVO: Botón para colapsar/expandir todas
        var toggleAllRect = new Rect(10f, inRect.height - 35f, 120f, 25f);
        bool anyExpanded = entryExpandedStates.Values.Any(expanded => expanded);
        string toggleText = anyExpanded ? $"📁 {"EchoColony.CollapseAll".Translate()}" : $"📂 {"EchoColony.ExpandAll".Translate()}";
        
        if (Widgets.ButtonText(toggleAllRect, toggleText))
        {
            bool newState = !anyExpanded;
            var keys = entryExpandedStates.Keys.ToList();
            foreach (var key in keys)
            {
                entryExpandedStates[key] = newState;
            }
        }

        // Botón Actualizar
        var refreshRect = new Rect(inRect.width - 130f, inRect.height - 35f, 120f, 25f);
        if (Widgets.ButtonText(refreshRect, $"🔄 {"EchoColony.RefreshButton".Translate()}"))
        {
            LoadMemories();
        }

        // Botón Limpiar Todo
        var clearRect = new Rect(inRect.width - 260f, inRect.height - 35f, 120f, 25f);
        if (Widgets.ButtonText(clearRect, $"🗑️ {"EchoColony.ClearAllMemories".Translate()}"))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.ClearAllMemoriesConfirm".Translate(pawn.LabelShort),
                () =>
                {
                    var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
                    tracker?.ClearAllMemories();
                    LoadMemories();
                    Messages.Message("EchoColony.MemoriesDeleted".Translate(pawn.LabelShort), MessageTypeDefOf.TaskCompletion);
                }));
        }

        // Info del día actual
        int currentDay = GenDate.DaysPassed;
        GUI.color = new Color(0.7f, 0.8f, 0.9f);
        Widgets.Label(new Rect(140f, inRect.height - 30f, 200f, 25f), $"📅 {"EchoColony.CurrentDay".Translate()} {currentDay}");
        GUI.color = Color.white;
    }

    private void DrawMemories(Rect contentRect)
    {
        float padding = 10f;
        var scrollRect = new Rect(contentRect.x + padding, contentRect.y + padding, 
                                 contentRect.width - padding * 2, contentRect.height - padding * 2);

        // ✅ ALTURA DINÁMICA: Calcular basado en estados de expansión
        float baseEntryHeight = 80f; // Altura colapsada
        float expandedEntryHeight = 180f; // Altura expandida
        float spacing = 15f;
        
        float totalHeight = 0f;
        foreach (var kvp in allMemories)
        {
            bool isExpanded = entryExpandedStates.ContainsKey(kvp.Key) ? entryExpandedStates[kvp.Key] : false;
            totalHeight += (isExpanded ? expandedEntryHeight : baseEntryHeight) + spacing;
        }
        
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalHeight);

        Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

        float y = 0f;
        int entryIndex = 0;
        
        foreach (var kvp in allMemories.OrderByDescending(k => k.Key))
        {
            int day = kvp.Key;
            string memory = kvp.Value ?? "";
            bool isExpanded = entryExpandedStates.ContainsKey(day) ? entryExpandedStates[day] : false;
            
            float currentEntryHeight = isExpanded ? expandedEntryHeight : baseEntryHeight;
            var entryRect = new Rect(0f, y, viewRect.width, currentEntryHeight);
            
            // Fondo alternado
            Color bgColor = entryIndex % 2 == 0 
                ? new Color(0.12f, 0.15f, 0.2f, 0.8f) 
                : new Color(0.08f, 0.12f, 0.18f, 0.8f);
            
            Widgets.DrawBoxSolid(entryRect, bgColor);
            
            // Borde izquierdo de color
            int daysDiff = GenDate.DaysPassed - day;
            Color borderColor = daysDiff == 0 ? Color.green :
                               daysDiff <= 3 ? Color.yellow :
                               daysDiff <= 7 ? Color.gray : Color.red;
            
            var borderRect = new Rect(0f, y, 4f, currentEntryHeight);
            Widgets.DrawBoxSolid(borderRect, borderColor);

            // ✅ HEADER CLICABLE para expandir/colapsar
            var headerRect = new Rect(15f, y + 8f, viewRect.width - 30f, 25f);
            
            // Detectar click en header
            if (Widgets.ButtonInvisible(headerRect))
            {
                entryExpandedStates[day] = !isExpanded;
            }

            // Icono de expansión
            string expandIcon = isExpanded ? "▼" : "▶";
            GUI.color = Color.white;
            Widgets.Label(new Rect(15f, y + 8f, 20f, 25f), expandIcon);
            
            // Fecha y antigüedad
            Text.Font = GameFont.Small;
            GUI.color = Color.cyan;
            string dayText = day == GenDate.DaysPassed ? "EchoColony.Today".Translate().ToString() : "EchoColony.Day".Translate().ToString() + " " + day;
            string ageText = daysDiff == 0 ? "" : 
                           daysDiff == 1 ? " " + "EchoColony.Yesterday".Translate().ToString() : 
                           $" ({daysDiff} " + "EchoColony.DaysAgo".Translate().ToString() + ")";
            Widgets.Label(new Rect(40f, y + 8f, 200f, 25f), $"📅 {dayText}{ageText}");
            
            // Indicador de fuente
            bool isGroupMemory = memory.StartsWith("[Conversación grupal") || 
                               memory.Contains("conversación grupal") || 
                               memory.Contains("Conversación grupal") ||
                               ContainsMultipleColonistNames(memory);
            
            GUI.color = isGroupMemory 
                ? new Color(0.7f, 0.9f, 1f)
                : new Color(0.9f, 1f, 0.7f);
            
            string sourceIcon = isGroupMemory ? "👥" : "💬";
            Widgets.Label(new Rect(viewRect.width - 50f, y + 8f, 40f, 25f), sourceIcon);
            GUI.color = Color.white;

            // ✅ CONTENIDO EXPANDIBLE
            if (isExpanded)
            {
                // Área para el contenido de la memoria con scroll propio
                var memoryContentRect = new Rect(15f, y + 40f, viewRect.width - 30f, expandedEntryHeight - 50f);
                
                DrawMemoryContent(memoryContentRect, day, memory);
            }
            else
            {
                // Vista previa en modo colapsado
                var previewRect = new Rect(15f, y + 35f, viewRect.width - 30f, 35f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                
                string preview = memory.Length > 100 ? memory.Substring(0, 100) + "..." : memory;
                // Remover saltos de línea para preview
                preview = preview.Replace("\n", " ").Replace("\r", "");
                
                Widgets.Label(previewRect, preview);
                GUI.color = Color.white;
            }

            y += currentEntryHeight + spacing;
            entryIndex++;
        }

        Widgets.EndScrollView();
        Text.Font = GameFont.Small;
    }

    // ✅ NUEVO MÉTODO: Dibujar contenido de memoria con scroll propio
    // ✅ MÉTODO CORREGIDO: Dibujar contenido de memoria con scroll funcional
private void DrawMemoryContent(Rect contentRect, int day, string memory)
{
    // Fondo para el área de contenido
    Widgets.DrawBoxSolid(contentRect, new Color(0.05f, 0.08f, 0.12f, 0.9f));
    
    // Área de scroll interna con padding
    var scrollArea = new Rect(contentRect.x + 5f, contentRect.y + 5f, 
                             contentRect.width - 10f, contentRect.height - 10f);
    
    // ✅ CALCULAR ALTURA REAL DEL TEXTO correctamente
    Text.Font = GameFont.Tiny;
    Text.WordWrap = true;
    
    // Ancho disponible para el texto (descontando scrollbar)
    float availableTextWidth = scrollArea.width - 20f; // 20f para la scrollbar
    
    // Calcular altura real necesaria para todo el texto
    float requiredTextHeight = Text.CalcHeight(memory, availableTextWidth);
    
    // ✅ ALTURA MÍNIMA para permitir scroll incluso con poco texto
    float minHeight = scrollArea.height + 10f;
    float finalTextHeight = Mathf.Max(requiredTextHeight + 20f, minHeight); // +20f padding extra
    
    var viewRect = new Rect(0f, 0f, availableTextWidth, finalTextHeight);
    
    // Obtener posición de scroll para esta entrada específica
    Vector2 currentScrollPos = entryScrollPositions.ContainsKey(day) ? entryScrollPositions[day] : Vector2.zero;
    
    // ✅ SCROLL VIEW con dimensiones correctas
    Widgets.BeginScrollView(scrollArea, ref currentScrollPos, viewRect);
    
    // ✅ ÁREA DE TEXTO que ocupa todo el viewRect
    var textRect = new Rect(0f, 0f, viewRect.width, viewRect.height);
    
    try
    {
        // ✅ TEXTO EDITABLE con configuración correcta
        Text.Font = GameFont.Tiny;
        Text.WordWrap = true;
        
        string newMemory = Widgets.TextArea(textRect, memory);
        
        if (newMemory != memory)
        {
            allMemories[day] = newMemory;
            // Guardar cambios inmediatamente
            var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
            tracker?.SaveMemoryForDay(day, newMemory);
            Log.Message("EchoColony.MemoryEdited".Translate(pawn.LabelShort, day));
        }
    }
    catch (System.Exception ex)
    {
        Log.Warning($"[EchoColony] Error en TextArea para memoria día {day}: {ex.Message}");
        
        // ✅ FALLBACK: Mostrar como texto de solo lectura si falla la edición
        GUI.color = new Color(0.9f, 0.9f, 0.9f);
        Widgets.Label(textRect, memory);
        GUI.color = Color.white;
    }
    
    Widgets.EndScrollView();
    
    // ✅ GUARDAR posición de scroll actualizada
    entryScrollPositions[day] = currentScrollPos;
    
    // ✅ BOTÓN DE BORRAR INDIVIDUAL (pequeño en esquina)
    var deleteRect = new Rect(contentRect.xMax - 25f, contentRect.y + 5f, 20f, 20f);
    GUI.color = new Color(1f, 0.4f, 0.4f);
    TooltipHandler.TipRegion(deleteRect, "EchoColony.DeleteMemoryTooltip".Translate());
    
    if (Widgets.ButtonText(deleteRect, "×"))
    {
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "EchoColony.DeleteMemoryConfirm".Translate(day),
            () =>
            {
                var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
                tracker?.RemoveMemoryForDay(day);
                allMemories.Remove(day);
                entryScrollPositions.Remove(day);
                entryExpandedStates.Remove(day);
                Messages.Message("EchoColony.MemoryDeleted".Translate(), MessageTypeDefOf.TaskCompletion);
            }));
    }
    GUI.color = Color.white;
    
    // ✅ RESTAURAR configuración de texto
    Text.WordWrap = false;
    Text.Font = GameFont.Small;
}
}