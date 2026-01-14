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
    private Dictionary<int, Vector2> entryScrollPositions = new Dictionary<int, Vector2>();
    private Dictionary<int, bool> entryExpandedStates = new Dictionary<int, bool>();
    
    // Debouncing system for memory editing
    private Dictionary<int, float> lastEditTimes = new Dictionary<int, float>();
    private Dictionary<int, string> pendingEdits = new Dictionary<int, string>();
    private const float EDIT_DEBOUNCE_TIME = 2.0f;

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
		//The memory continuity problem was solved by modifying this structure; however, modifications to other parts of the code cannot be ruled out. 
        var manager = Current.Game.GetComponent<ColonistMemoryManager>();
        var tracker = manager?.GetTrackerFor(pawn);
        allMemories = tracker?.GetAllMemories() ?? new Dictionary<int, string>();

        foreach (var day in allMemories.Keys)
        {
            if (!entryScrollPositions.ContainsKey(day))
                entryScrollPositions[day] = Vector2.zero;
            
            if (!entryExpandedStates.ContainsKey(day))
                entryExpandedStates[day] = false;
        }

        Log.Message($"[EchoColony] {"EchoColony.MemoriesLoaded".Translate(allMemories.Count, pawn.LabelShort)}");
    }

    // Process pending edits after debounce delay
    private void ProcessPendingEdits()
    {
        var keysToProcess = lastEditTimes
            .Where(kvp => Time.unscaledTime - kvp.Value > EDIT_DEBOUNCE_TIME)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var day in keysToProcess)
        {
            if (pendingEdits.ContainsKey(day))
            {
                var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
                if (tracker != null)
                {
                    // Update the memory with debounced edit
                    string originalMemory = allMemories.ContainsKey(day) ? allMemories[day] : "";
                    string newMemory = pendingEdits[day];
                    
                    // Only save if there's a meaningful change
                    if (!string.Equals(originalMemory.Trim(), newMemory.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        tracker.SaveMemoryForDay(day, newMemory);
                        Log.Message($"[EchoColony] Memory edited and saved for {pawn.LabelShort}, day {day}");
                    }
                }
                
                pendingEdits.Remove(day);
            }
            lastEditTimes.Remove(day);
        }
    }

    public override Vector2 InitialSize => new Vector2(750f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        // Process any pending edits first
        //ProcessPendingEdits();

        // Header
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

        // Content area
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

        // Footer
        var footerRect = new Rect(0f, inRect.height - 40f, inRect.width, 35f);
        Widgets.DrawBoxSolid(footerRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Toggle all button
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

        // Refresh button
        var refreshRect = new Rect(inRect.width - 130f, inRect.height - 35f, 120f, 25f);
        if (Widgets.ButtonText(refreshRect, $"🔄 {"EchoColony.RefreshButton".Translate()}"))
        {
            LoadMemories();
        }

        // Clear all button
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

        // Current day info
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

        // Calculate dynamic height based on expansion states
        float baseEntryHeight = 80f;
        float expandedEntryHeight = 180f;
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
            
            // Alternating background
            Color bgColor = entryIndex % 2 == 0 
                ? new Color(0.12f, 0.15f, 0.2f, 0.8f) 
                : new Color(0.08f, 0.12f, 0.18f, 0.8f);
            
            Widgets.DrawBoxSolid(entryRect, bgColor);
            
            // Colored left border
            int daysDiff = GenDate.DaysPassed - day;
            Color borderColor = daysDiff == 0 ? Color.green :
                               daysDiff <= 3 ? Color.yellow :
                               daysDiff <= 7 ? Color.gray : Color.red;
            
            var borderRect = new Rect(0f, y, 4f, currentEntryHeight);
            Widgets.DrawBoxSolid(borderRect, borderColor);

            // Clickable header for expand/collapse
            var headerRect = new Rect(15f, y + 8f, viewRect.width - 200f, 25f); //Reduced area to make space for buttons
            
            if (Widgets.ButtonInvisible(headerRect))
            {
                entryExpandedStates[day] = !isExpanded;
            }

            // Expansion icon
            string expandIcon = isExpanded ? "▼" : "▶";
            GUI.color = Color.white;
            Widgets.Label(new Rect(15f, y + 8f, 20f, 25f), expandIcon);
            
            // Date and age
            Text.Font = GameFont.Small;
            GUI.color = Color.cyan;
            string dayText = day == GenDate.DaysPassed ? "EchoColony.Today".Translate().ToString() : "EchoColony.Day".Translate().ToString() + " " + day;
            string ageText = daysDiff == 0 ? "" : 
                           daysDiff == 1 ? " " + "EchoColony.Yesterday".Translate().ToString() : 
                           $" ({daysDiff} " + "EchoColony.DaysAgo".Translate().ToString() + ")";
            Widgets.Label(new Rect(40f, y + 8f, 200f, 25f), $"📅 {dayText}{ageText}");
            
            // Source indicator
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

            // Expandable content
            if (isExpanded)
            {
                var memoryContentRect = new Rect(15f, y + 40f, viewRect.width - 30f, expandedEntryHeight - 50f);
                DrawMemoryContent(memoryContentRect, day, memory);
            }
            else
            {
                // Collapsed preview
                var previewRect = new Rect(15f, y + 35f, viewRect.width - 30f, 35f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                
                string preview = memory.Length > 100 ? memory.Substring(0, 100) + "..." : memory;
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

    // Draw memory content with debounced editing
    private void DrawMemoryContent(Rect contentRect, int day, string memory)
    {
        Widgets.DrawBoxSolid(contentRect, new Color(0.05f, 0.08f, 0.12f, 0.9f));
        
        var scrollArea = new Rect(contentRect.x + 5f, contentRect.y + 5f, 
                                 contentRect.width - 15f, contentRect.height - 10f);
        
        Text.Font = GameFont.Tiny;
        Text.WordWrap = true;
        
        float availableTextWidth = scrollArea.width - 20f;
        float requiredTextHeight = Text.CalcHeight(memory, availableTextWidth);
        float minHeight = scrollArea.height + 10f;
        float finalTextHeight = Mathf.Max(requiredTextHeight + 20f, minHeight);
        
        var viewRect = new Rect(0f, 0f, availableTextWidth, finalTextHeight);
        
        Vector2 currentScrollPos = entryScrollPositions.ContainsKey(day) ? entryScrollPositions[day] : Vector2.zero;
        
        Widgets.BeginScrollView(scrollArea, ref currentScrollPos, viewRect);
        
        var textRect = new Rect(0f, 0f, viewRect.width, viewRect.height);
        
        try
        {
            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            
            string newMemory = Widgets.TextArea(textRect, memory);
            
            if (newMemory != memory)
            {
                allMemories[day] = newMemory;
                
                // Add to pending edits with debouncing
                pendingEdits[day] = newMemory;
                //lastEditTimes[day] = Time.unscaledTime;

                //var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);
                //tracker?.UpdateMemory(day, newMemory);

                //pendingEdits[day] = newMemory;

                // Show visual indicator that edit is pending
                //if (pendingEdits.ContainsKey(day))
                //{
                //    GUI.color = new Color(1f, 1f, 0f, 0.3f);
                //    Widgets.DrawBoxSolid(new Rect(textRect.x - 2f, textRect.y - 2f, textRect.width + 4f, 20f), GUI.color);
                //    GUI.color = Color.white;
                //}
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[EchoColony] Error in TextArea for memory day {day}: {ex.Message}");
            
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(textRect, memory);
            GUI.color = Color.white;
        }
        
        Widgets.EndScrollView();
        
        entryScrollPositions[day] = currentScrollPos;

        // Individual delete button

        //float buttonStartX = contentRect.xMax - 30f;
        var deleteRect = new Rect(contentRect.xMax - 23f, contentRect.y - 25f, 20f, 20f);
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
                    
                    // Clean up any pending edits for this entry
                    if (pendingEdits.ContainsKey(day))
                        pendingEdits.Remove(day);
                    if (lastEditTimes.ContainsKey(day))
                        lastEditTimes.Remove(day);
                        
                    Messages.Message("EchoColony.MemoryDeleted".Translate(), MessageTypeDefOf.TaskCompletion);
                }));
        }
        GUI.color = Color.white;
        

        Text.WordWrap = false;
        Text.Font = GameFont.Small;

        // BOTÓN PROCESAR IA (✨)
        // Se coloca a la izquierda del botón eliminar
        var aiBtnRect = new Rect(deleteRect.x - 105f, contentRect.y - 25f, 100f, 20f);

        bool hasChanges = pendingEdits.ContainsKey(day);
        if (hasChanges) GUI.color = new Color(0.6f, 0.8f, 1f); // Azul claro si hay algo que procesar

        if (Widgets.ButtonText(aiBtnRect, "✨ Process whit IA "))
        {
            if (pendingEdits.ContainsKey(day))
            {
                var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);

                // Llamada limpia: solo el día y el texto editado
                tracker?.OptimizeCustomMemoryWithAI(day, pendingEdits[day]);

                pendingEdits.Remove(day);
                Messages.Message("La IA está personificando tu nota...", MessageTypeDefOf.TaskCompletion);
            }
        }
        GUI.color = Color.white;
    }

    public override void PostClose()
    {
        base.PostClose();

        // 1. Obtenemos el tracker del colono
        var tracker = MyStoryModComponent.Instance?.ColonistMemoryManager?.GetTrackerFor(pawn);

        if (tracker != null && pendingEdits.Count > 0)
        {
            int count = 0;
            // 2. Recorremos todos los cambios que se quedaron en "pendientes"
            foreach (var edit in pendingEdits)
            {
                tracker.UpdateMemory(edit.Key, edit.Value);
                count++;
            }

            // 3. Opcional: Log para confirmar que se guardó al salir
            Log.Message($"[EchoColony] Guardado finalizado: {count} memorias actualizadas localmente al cerrar ventana.");

            // Limpiamos los pendientes ya procesados
            pendingEdits.Clear();
        }
    }
}