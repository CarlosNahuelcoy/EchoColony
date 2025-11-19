using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using RimWorld;
using System.Text;
using System.IO;
using System;

namespace EchoColony
{
    public class ColonistGroupChatWindow : Window
    {
        private List<Pawn> participants;
        private GroupChatSession session;
        private Dictionary<Pawn, int> turnTracker = new Dictionary<Pawn, int>();
        private Vector2 scrollPos = Vector2.zero;
        private Vector2 participantsScrollPos = Vector2.zero;
        private string userMessage = "";
        private bool sendRequestedViaEnter = false;

        private HashSet<Pawn> KickedOut => session.KickedOutColonists;

        private Pawn initiator;

        
        // ✅ NUEVAS VARIABLES PARA GESTIÓN DE PARTICIPANTES
        private List<Pawn> availableColonists = new List<Pawn>();
        private bool showParticipantManagement = false;

        private const float MAX_CHAT_DISTANCE = 15f;
        private const float OUTDOOR_CHAT_DISTANCE = 8f;
        private const int MAX_NEW_JOINERS = 2;

        public ColonistGroupChatWindow(List<Pawn> participants)
{
    this.initiator = participants.First();
    
    // ✅ OBTENER SESIÓN PRIMERO para acceder a KickedOut
    this.session = GroupChatGameComponent.Instance.GetOrCreateSession(participants);
    
    // ✅ Asegurar que KickedOut esté inicializado
    if (this.session.KickedOutColonists == null)
        this.session.KickedOutColonists = new HashSet<Pawn>();
    
    // ✅ FILTRAR participantes - excluir a los expulsados previamente
    this.participants = participants.Distinct()
        .Where(p => !this.session.KickedOutColonists.Contains(p))
        .ToList();
    
    // ✅ Si quedamos sin participantes o solo queda uno, recuperar al iniciador
    if (this.participants.Count == 0 || !this.participants.Contains(this.initiator))
    {
        this.participants.Clear();
        this.participants.Add(this.initiator);
        // El iniciador nunca puede estar en KickedOut
        this.session.KickedOutColonists.Remove(this.initiator);
    }
    
    // ✅ ACTUALIZAR la sesión con los participantes filtrados
    this.session = GroupChatGameComponent.Instance.GetOrCreateSession(this.participants);

    this.doCloseX = true;
    this.forcePause = true;
    this.absorbInputAroundWindow = true;
    this.closeOnClickedOutside = true;
    this.closeOnAccept = false;
    
    // ✅ INICIALIZAR LISTA DE COLONOS DISPONIBLES
    UpdateAvailableColonists();
}

        // ✅ TAMAÑO INICIAL MÁS GRANDE PARA ACOMODAR NUEVAS FUNCIONES
        public override Vector2 InitialSize => new Vector2(1100f, 700f);

        // ✅ ACTUALIZAR COLONOS DISPONIBLES BAJO DEMANDA
        private void UpdateAvailableColonists()
{
    if (participants == null || participants.Count == 0)
    {
        availableColonists.Clear();
        return;
    }

    IntVec3 center = CalculateConversationCenter();
    if (!center.IsValid)
    {
        availableColonists.Clear();
        return;
    }

    Map map = participants[0].Map;
    if (map == null)
    {
        availableColonists.Clear();
        return;
    }

    // ✅ LIMPIAR lista de expulsados primero
    CleanupKickedOutList();

    // Solo excluir a los participantes actuales Y a los expulsados manualmente
    var candidates = GetChatEligibleColonistsFromCenter(center, map, participants)
        .Where(p => !KickedOut.Contains(p)) // ✅ Excluir expulsados manualmente
        .ToList();

    // ✅ OPCIONAL: Incluir expulsados que el jugador quiera reintegrar manualmente
    // (esto los mostraría en la lista con un indicador especial)
    var kickedNearby = map.mapPawns.FreeColonistsSpawned
        .Where(p => KickedOut.Contains(p))
        .Where(p => IsBasicEligibleFromCenter(p, center, map) && CanCommunicateFromCenter(center, map, p))
        .ToList();

    availableColonists = candidates.ToList();
    
    // ✅ Si quieres mostrar expulsados con un indicador especial, descomenta:
    // availableColonists.AddRange(kickedNearby);

    Log.Message($"[EchoColony] 📌 Disponibles: {string.Join(", ", availableColonists.Select(p => p.LabelShort))} | Expulsados: {string.Join(", ", KickedOut.Select(p => p.LabelShort))}");
}

        private IntVec3 CalculateConversationCenter()
        {
            if (participants.Count == 0) return IntVec3.Invalid;
            if (participants.Count == 1) return participants[0].Position;
            
            return CellRect.FromLimits(
                participants.Min(p => p.Position.x), participants.Min(p => p.Position.z),
                participants.Max(p => p.Position.x), participants.Max(p => p.Position.z)
            ).CenterCell;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float currentY = 10f;
            
            // ✅ TÍTULO Y CONTROLES SUPERIORES
            DrawHeader(inRect, ref currentY);
            
            // ✅ GESTIÓN DE PARTICIPANTES (OPCIONAL)
            if (showParticipantManagement)
            {
                DrawParticipantManagement(inRect, ref currentY);
            }
            
            // ✅ ÁREA DE CHAT
            DrawChatArea(inRect, ref currentY);
            
            // ✅ CONTROLES INFERIORES (INPUT, BOTONES)
            DrawBottomControls(inRect, currentY);
        }

        private void DrawHeader(Rect inRect, ref float currentY)
        {
            // ✅ BOTONES EXPORT Y CLEAR EN ESQUINA SUPERIOR IZQUIERDA
            float buttonWidth = 70f;
            float buttonSpacing = 5f;
            
            Rect exportBtn = new Rect(10f, currentY, buttonWidth, 25f);
            if (Widgets.ButtonText(exportBtn, "Export"))
            {
                ExportChat();
            }

            Rect clearBtn = new Rect(exportBtn.xMax + buttonSpacing, currentY, buttonWidth, 25f);
            GUI.color = new Color(1f, 0.7f, 0.7f); // Tono rojizo suave
            if (Widgets.ButtonText(clearBtn, "Clear"))
            {
                ClearChat();
            }
            GUI.color = Color.white;

            // Título principal (centrado)
            Text.Font = GameFont.Medium;
            string label = "EchoColony.GroupChatLabel".Translate();
            string names = string.Join(", ", participants.Select(p => p.LabelShort));
            float titleStartX = clearBtn.xMax + 20f;
            float titleWidth = inRect.width - titleStartX - 150f;
            Widgets.Label(new Rect(titleStartX, currentY, titleWidth, 30f), $"{label} {names}");
            
            // ✅ BOTÓN PARA GESTIÓN DE PARTICIPANTES (esquina superior derecha)
            Rect participantBtn = new Rect(inRect.width - 140f, currentY, 130f, 25f);
            if (Widgets.ButtonText(participantBtn, showParticipantManagement ? "Hide Members" : "Manage Members"))
            {
                showParticipantManagement = !showParticipantManagement;
                // Actualizar lista solo cuando se abre el panel
                if (showParticipantManagement)
                {
                    UpdateAvailableColonists();
                }
            }
            
            currentY += 35f;
            Text.Font = GameFont.Small;

            // ✅ RETRATOS DE PARTICIPANTES ACTUALES
            DrawCurrentParticipants(inRect, ref currentY);
        }

        private void DrawCurrentParticipants(Rect inRect, ref float currentY)
        {
            float portraitSize = 60f;
            float spacing = 15f;
            float totalWidth = participants.Count * (portraitSize + spacing);
            float startX = (inRect.width - totalWidth) / 2f;

            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                float x = startX + i * (portraitSize + spacing);
                
                // Retrato
                GUI.DrawTexture(new Rect(x, currentY, portraitSize, portraitSize), 
                    PortraitsCache.Get(p, new Vector2(portraitSize, portraitSize), Rot4.South, default, 1.25f));
                
                // Nombre
                Widgets.Label(new Rect(x, currentY + portraitSize + 2f, portraitSize + 40f, 20f), p.LabelShort);
                
                // ✅ BOTÓN PARA REMOVER (solo si hay más de 1 participante)
                if (participants.Count >= 3 && p != initiator)
                {
                    Rect removeBtn = new Rect(x + portraitSize - 15f, currentY - 5f, 20f, 20f);
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(removeBtn, "×"))
                    {
                        RemoveParticipant(p);
                    }
                    GUI.color = Color.white;
                }
            }
            
            currentY += portraitSize + 25f;
        }

        // ✅ MÉTODO MEJORADO: DrawParticipantManagement con indicadores visuales
private void DrawParticipantManagement(Rect inRect, ref float currentY)
{
    // ✅ ÁREA DE GESTIÓN DE PARTICIPANTES
    Rect managementRect = new Rect(10f, currentY, inRect.width - 20f, 160f); // Más alto para expulsados
    Widgets.DrawBoxSolid(managementRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
    
    // Título con contador
    Text.Font = GameFont.Small;
    string title = $"Available Colonists Nearby: ({availableColonists.Count})";
    Widgets.Label(new Rect(managementRect.x + 10f, managementRect.y + 5f, 300f, 25f), title);
    
    // ✅ MOSTRAR EXPULSADOS si los hay
    if (KickedOut.Count > 0)
    {
        string kickedTitle = $"Previously Removed: ({KickedOut.Count})";
        GUI.color = new Color(1f, 0.7f, 0.7f); // Color rojizo
        Widgets.Label(new Rect(managementRect.x + 320f, managementRect.y + 5f, 200f, 25f), kickedTitle);
        GUI.color = Color.white;
    }
    
    // ✅ SCROLL AREA PARA COLONOS DISPONIBLES
    Rect scrollRect = new Rect(managementRect.x + 10f, managementRect.y + 30f, 
        managementRect.width - 20f, 120f);
    
    float itemHeight = 25f;
    
    // ✅ COMBINAR disponibles y expulsados para mostrar todo
    var allColonistsToShow = new List<(Pawn pawn, bool isKicked)>();
    
    // Agregar disponibles
    foreach (var colonist in availableColonists)
    {
        allColonistsToShow.Add((colonist, false));
    }
    
    // Agregar expulsados que estén cerca
    var kickedNearby = KickedOut
        .Where(p => p?.Map == participants?.FirstOrDefault()?.Map && !p.Dead)
        .Where(p => IsBasicEligibleFromCenter(p, CalculateConversationCenter(), p.Map))
        .ToList();
    
    foreach (var kicked in kickedNearby)
    {
        allColonistsToShow.Add((kicked, true));
    }
    
    float totalHeight = allColonistsToShow.Count * itemHeight;
    Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, totalHeight);
    
    Widgets.BeginScrollView(scrollRect, ref participantsScrollPos, viewRect);
    
    for (int i = 0; i < allColonistsToShow.Count; i++)
    {
        var (colonist, isKicked) = allColonistsToShow[i];
        Rect itemRect = new Rect(0, i * itemHeight, viewRect.width, itemHeight - 2f);
        
        // ✅ Fondo especial para expulsados
        if (isKicked)
        {
            Widgets.DrawBoxSolid(itemRect, new Color(0.5f, 0.2f, 0.2f, 0.3f)); // Fondo rojizo
        }
        else if (i % 2 == 0)
        {
            Widgets.DrawBoxSolid(itemRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
        }
        
        // ✅ Indicador visual para expulsados
        if (isKicked)
        {
            GUI.color = Color.red;
            Widgets.Label(new Rect(itemRect.x + 5f, itemRect.y, 20f, itemRect.height), "✗");
            GUI.color = Color.white;
        }
        
        // Nombre del colono (con offset si está expulsado)
        float nameOffset = isKicked ? 25f : 5f;
        GUI.color = isKicked ? new Color(1f, 0.8f, 0.8f) : Color.white;
        Widgets.Label(new Rect(itemRect.x + nameOffset, itemRect.y, 130f, itemRect.height), 
            colonist.LabelShort);
        GUI.color = Color.white;
        
        // Distancia
        float distance = colonist.Position.DistanceTo(CalculateConversationCenter());
        Widgets.Label(new Rect(itemRect.x + 160f, itemRect.y, 80f, itemRect.height), 
            $"{distance:F1}m");
        
        // ✅ BOTÓN CONTEXTUAL
        Rect actionBtn = new Rect(itemRect.x + itemRect.width - 80f, itemRect.y + 2f, 75f, 20f);
        
        if (isKicked)
        {
            // Botón para reintegrar
            GUI.color = Color.yellow;
            if (Widgets.ButtonText(actionBtn, "Readd"))
            {
                AddParticipant(colonist);
            }
        }
        else
        {
            // Botón para añadir normal
            GUI.color = Color.green;
            if (Widgets.ButtonText(actionBtn, "Add"))
            {
                AddParticipant(colonist);
            }
        }
        GUI.color = Color.white;
    }
    
    Widgets.EndScrollView();
    
    currentY += managementRect.height + 10f;
}
        private void DrawChatArea(Rect inRect, ref float currentY)
        {
            float chatHeight = inRect.height - currentY - 80f; // Espacio para controles inferiores
            Rect scrollRect = new Rect(10f, currentY, inRect.width - 20f, chatHeight);
            
            // ✅ CALCULAR ALTURAS DE MENSAJES
            List<float> heights = new List<float>();
            foreach (var msg in session.History)
            {
                string displayMsg = GetDisplayMessage(msg);
                float width = scrollRect.width - 16f;
                heights.Add(Text.CalcHeight(displayMsg, width));
            }
            float viewHeight = heights.Sum() + (heights.Count * 10f);
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);
            float y = 0;
            for (int i = 0; i < session.History.Count; i++)
            {
                var msg = session.History[i];

                if (msg.StartsWith("[DATE_SEPARATOR]"))
                {
                    DrawDateSeparator(new Rect(0, y, viewRect.width, heights[i]), msg);
                }
                else
                {
                    Rect labelRect = new Rect(0, y, viewRect.width, heights[i]);
                    string displayMsg = GetDisplayMessage(msg);
                    Widgets.Label(labelRect, displayMsg);
                }

                y += heights[i] + 10f;
            }
            Widgets.EndScrollView();
            
            currentY += chatHeight + 10f;
        }

        // ✅ CORREGIDO: Solo botón Send en la parte inferior
        private void DrawBottomControls(Rect inRect, float currentY)
        {
            float bottomHeight = 60f;
            float buttonWidth = 80f;
            float buttonSpacing = 10f;
            
            // ✅ MANEJO DE ENTRADA CON ENTER
            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "GroupChatInputField" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            // ✅ ÁREA DE TEXTO (ahora más grande)
            Rect inputRect = new Rect(10f, currentY, inRect.width - buttonWidth - buttonSpacing - 20f, bottomHeight);
            GUI.SetNextControlName("GroupChatInputField");

            var textStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding = new RectOffset(6, 6, 6, 6)
            };
            userMessage = GUI.TextArea(inputRect, userMessage, 500, textStyle);

            // ✅ SOLO BOTÓN SEND
            Rect sendBtn = new Rect(inputRect.xMax + buttonSpacing, currentY, buttonWidth, bottomHeight);
            bool sendClicked = Widgets.ButtonText(sendBtn, "Send");

            // ✅ PROCESAR ENVÍO DE MENSAJE
            if ((sendClicked || sendRequestedViaEnter) && !userMessage.NullOrEmpty())
            {
                AddUserLine("You:: " + userMessage);
                StartGroupConversation(userMessage);
                userMessage = "";
                sendRequestedViaEnter = false;
                GUI.FocusControl(null);
            }
        }

        // ✅ CORREGIDO: AddParticipant ahora actualiza correctamente la lista
    private void AddParticipant(Pawn newParticipant)
{
    if (participants.Contains(newParticipant)) return;

    // ✅ IMPORTANTE: Remover de expulsados ANTES de agregar
    KickedOut.Remove(newParticipant);
    participants.Add(newParticipant);

    // ✅ ACTUALIZAR la sesión para que refleje los nuevos participantes
    session = GroupChatGameComponent.Instance.GetOrCreateSession(participants);

    string joinMessage = "EchoColony.ColonistJoinsManually".Translate(newParticipant.LabelShort);
    session.AddMessage(joinMessage);

    UpdateAvailableColonists();
    scrollPos.y = float.MaxValue;
}


        // ✅ CORREGIDO: RemoveParticipant ahora actualiza correctamente la lista
        private void RemoveParticipant(Pawn participantToRemove)
        {
            if (participants.Count < 3) return;

            if (participantToRemove == initiator)
            {
                Messages.Message("You can't remove the initiator of the conversation.", MessageTypeDefOf.RejectInput);
                return;
            }

            participants.Remove(participantToRemove);

            // ✅ IMPORTANTE: Agregar a expulsados para persistencia
            KickedOut.Add(participantToRemove);

            // ✅ ACTUALIZAR la sesión para que refleje los participantes restantes
            session = GroupChatGameComponent.Instance.GetOrCreateSession(participants);

            string leaveMessage = "EchoColony.ColonistLeavesManually".Translate(participantToRemove.LabelShort);
            session.AddMessage(leaveMessage);

            UpdateAvailableColonists();
            scrollPos.y = float.MaxValue;
        }

// ✅ NUEVO MÉTODO: Verificar si un colono fue expulsado manualmente
private bool WasManuallyKicked(Pawn pawn)
{
    return KickedOut.Contains(pawn);
}

private void CleanupKickedOutList()
{
    if (KickedOut == null) return;
    
    var toRemove = KickedOut.Where(p => p.Dead || p.Map == null || p.Map != participants.FirstOrDefault()?.Map).ToList();
    foreach (var pawn in toRemove)
    {
        KickedOut.Remove(pawn);
    }
}


        // ✅ MÉTODO PARA EXPORTAR CHAT
        private void ExportChat()
        {
            try
            {
                string fileName = $"GroupChat_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                string folderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "EchoColony", "ChatExports");

                // Crear directorio si no existe
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);

                // Preparar contenido
                var sb = new StringBuilder();
                sb.AppendLine("=== ECHOCOLONY GROUP CHAT EXPORT ===");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Participants: {string.Join(", ", participants.Select(p => p.LabelShort))}");
                sb.AppendLine("=" + new string('=', 40));
                sb.AppendLine();

                foreach (var message in session.History)
                {
                    if (!message.StartsWith("[DATE_SEPARATOR]"))
                    {
                        sb.AppendLine(GetDisplayMessage(message));
                    }
                }

                // Escribir archivo
                File.WriteAllText(filePath, sb.ToString());

                // Notificación al jugador
                Messages.Message($"Chat exported to: {fileName}", MessageTypeDefOf.PositiveEvent);

                // Opcional: Abrir carpeta (solo en Windows)
                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] Error exporting chat: {ex.Message}");
                Messages.Message("Failed to export chat. Check logs for details.", MessageTypeDefOf.NegativeEvent);
            }
        }
        // ✅ MÉTODO PARA LIMPIAR CHAT
        private void ClearChat()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Are you sure you want to clear the entire chat history? This cannot be undone.",
                delegate
                {
                    session.History.Clear();
                    session.AddMessage("EchoColony.ChatCleared".Translate());
                    Messages.Message("Chat history cleared.", MessageTypeDefOf.NeutralEvent);
                }
            ));
        }

        // ✅ MÉTODOS DE DETECCIÓN INTELIGENTE (MANTENER LOS EXISTENTES)
        private List<Pawn> GetChatEligibleColonists(Pawn centerPawn, bool excludeCenter = true)
        {
            if (centerPawn?.Map == null) return new List<Pawn>();

            var eligibleColonists = new List<Pawn>();
            var allColonists = centerPawn.Map.mapPawns.FreeColonistsSpawned
                .Where(p => IsBasicEligible(p, centerPawn, excludeCenter))
                .ToList();

            foreach (var colonist in allColonists)
            {
                if (CanCommunicate(centerPawn, colonist))
                {
                    eligibleColonists.Add(colonist);
                }
            }

            return eligibleColonists;
        }

        private List<Pawn> GetChatEligibleColonistsFromCenter(IntVec3 center, Map map, List<Pawn> excludeList = null)
        {
            if (map == null) return new List<Pawn>();
            
            excludeList = excludeList ?? new List<Pawn>();
            var eligibleColonists = new List<Pawn>();
            
            var allColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => IsBasicEligibleFromCenter(p, center, map) && !excludeList.Contains(p))
                .ToList();

            foreach (var colonist in allColonists)
            {
                if (CanCommunicateFromCenter(center, map, colonist))
                {
                    eligibleColonists.Add(colonist);
                }
            }

            return eligibleColonists;
        }

        private bool IsBasicEligible(Pawn pawn, Pawn centerPawn, bool excludeCenter)
        {
            if (pawn == null || centerPawn == null) return false;
            if (excludeCenter && pawn == centerPawn) return false;
            if (pawn.Dead || pawn.Map != centerPawn.Map) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;
            
            float distance = pawn.Position.DistanceTo(centerPawn.Position);
            return distance <= MAX_CHAT_DISTANCE;
        }

        private bool IsBasicEligibleFromCenter(Pawn pawn, IntVec3 center, Map map)
        {
            if (pawn == null || map == null) return false;
            if (pawn.Dead || pawn.Map != map) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;
            
            float distance = pawn.Position.DistanceTo(center);
            return distance <= MAX_CHAT_DISTANCE;
        }

        private bool CanCommunicate(Pawn speaker, Pawn listener)
        {
            if (speaker?.Map == null || listener?.Map == null) return false;
            if (speaker.Map != listener.Map) return false;

            IntVec3 speakerPos = speaker.Position;
            IntVec3 listenerPos = listener.Position;
            Map map = speaker.Map;

            Room speakerRoom = speakerPos.GetRoom(map);
            Room listenerRoom = listenerPos.GetRoom(map);

            if (speakerRoom != null && listenerRoom != null && speakerRoom == listenerRoom)
            {
                return true;
            }

            if (speakerRoom == null && listenerRoom == null)
            {
                float distance = speakerPos.DistanceTo(listenerPos);
                if (distance > OUTDOOR_CHAT_DISTANCE) return false;
                
                return GenSight.LineOfSight(speakerPos, listenerPos, map, skipFirstCell: true);
            }

            if ((speakerRoom == null) != (listenerRoom == null))
            {
                float distance = speakerPos.DistanceTo(listenerPos);
                if (distance > OUTDOOR_CHAT_DISTANCE) return false;

                return HasDirectConnection(speakerPos, listenerPos, map);
            }

            if (speakerRoom != listenerRoom)
            {
                float distance = speakerPos.DistanceTo(listenerPos);
                if (distance > OUTDOOR_CHAT_DISTANCE) return false;

                return AreRoomsConnected(speakerRoom, listenerRoom, map);
            }

            return false;
        }

        private bool CanCommunicateFromCenter(IntVec3 center, Map map, Pawn colonist)
        {
            if (map == null || colonist?.Map != map) return false;

            IntVec3 colonistPos = colonist.Position;
            
            Room centerRoom = center.GetRoom(map);
            Room colonistRoom = colonistPos.GetRoom(map);

            if (centerRoom != null && colonistRoom != null && centerRoom == colonistRoom)
            {
                return true;
            }

            if (centerRoom == null && colonistRoom == null)
            {
                float distance = center.DistanceTo(colonistPos);
                if (distance > OUTDOOR_CHAT_DISTANCE) return false;
                return GenSight.LineOfSight(center, colonistPos, map, skipFirstCell: true);
            }

            float dist = center.DistanceTo(colonistPos);
            if (dist > OUTDOOR_CHAT_DISTANCE) return false;

            if ((centerRoom == null) != (colonistRoom == null))
            {
                return HasDirectConnection(center, colonistPos, map);
            }

            if (centerRoom != colonistRoom)
            {
                return AreRoomsConnected(centerRoom, colonistRoom, map);
            }

            return false;
        }

        private bool HasDirectConnection(IntVec3 from, IntVec3 to, Map map)
        {
            if (!GenSight.LineOfSight(from, to, map, skipFirstCell: true)) return false;

            var cellsInPath = GenSight.PointsOnLineOfSight(from, to).ToList();
            
            foreach (var cell in cellsInPath)
            {
                if (!cell.InBounds(map)) continue;

                Building door = cell.GetDoor(map);
                if (door != null)
                {
                    if (door is Building_Door buildingDoor)
                    {
                        return buildingDoor.Open;
                    }
                }

                if (cell.Filled(map))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreRoomsConnected(Room room1, Room room2, Map map)
        {
            if (room1 == null || room2 == null) return false;

            var doors1 = GetRoomDoors(room1);
            var doors2 = GetRoomDoors(room2);

            foreach (var door1 in doors1)
            {
                foreach (var door2 in doors2)
                {
                    if (door1 == door2 && door1 is Building_Door buildingDoor)
                    {
                        return buildingDoor.Open;
                    }
                }
            }

            var bordesCells1 = room1.BorderCells.ToList();
            var bordesCells2 = room2.BorderCells.ToList();

            foreach (var cell1 in bordesCells1)
            {
                foreach (var cell2 in bordesCells2)
                {
                    if (cell1.AdjacentTo8Way(cell2))
                    {
                        if (!cell1.Filled(map) && !cell2.Filled(map))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private List<Building> GetRoomDoors(Room room)
        {
            var doors = new List<Building>();
            
            if (room?.BorderCells == null) return doors;

            foreach (var cell in room.BorderCells)
            {
                Building door = cell.GetDoor(room.Map);
                if (door != null && !doors.Contains(door))
                {
                    doors.Add(door);
                }
            }

            return doors;
        }

        // ✅ RESTO DE MÉTODOS EXISTENTES (StartGroupConversation, etc.)
        // [Mantener todos los métodos existentes del código original desde aquí...]
        
        private void AddUserLine(string line)
        {
            session.AddMessage(line);
        }

        private void StartGroupConversation(string message)
        {
            if (MyStoryModComponent.Instance == null)
            {
                Log.Error("[EchoColony] ❌ MyStoryModComponent.Instance es NULL!");
                return;
            }

            if (MyStoryModComponent.Instance.ColonistMemoryManager == null)
            {
                MyStoryModComponent.Instance.ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
                if (MyStoryModComponent.Instance.ColonistMemoryManager == null)
                {
                    MyStoryModComponent.Instance.ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                    Current.Game.components.Add(MyStoryModComponent.Instance.ColonistMemoryManager);
                }
            }

            MyStoryModComponent.Instance.StartCoroutine(GroupChatCoroutine(new List<Pawn>(participants), message));
        }

        private IEnumerator GroupChatCoroutine(List<Pawn> group, string message)
        {
            IntVec3 center = CellRect.FromLimits(
                group.Min(p => p.Position.x), group.Min(p => p.Position.z),
                group.Max(p => p.Position.x), group.Max(p => p.Position.z)
            ).CenterCell;

            Map map = group[0].Map;

            // VALIDACIÓN INTELIGENTE - remover colonos que ya no pueden comunicarse
            for (int i = group.Count - 1; i >= 0; i--)
            {
                Pawn p = group[i];
                if (p.Dead || p.Map != map)
                {
                    session.AddMessage("EchoColony.ColonistNoLongerPresent".Translate(p.LabelShort));
                    group.RemoveAt(i);
                    continue;
                }

                if (!CanCommunicateFromCenter(center, map, p))
                {
                    session.AddMessage("EchoColony.ColonistMovedAway".Translate(p.LabelShort));
                    group.RemoveAt(i);
                }
            }

            if (group.Count == 0)
            {
                session.AddMessage("EchoColony.AllColonistsGone".Translate());
                yield break;
            }

            // Recalcular centro después de posibles remociones
            if (group.Count > 1)
            {
                center = CellRect.FromLimits(
                    group.Min(p => p.Position.x), group.Min(p => p.Position.z),
                    group.Max(p => p.Position.x), group.Max(p => p.Position.z)
                ).CenterCell;
            }
            else
            {
                center = group[0].Position;
            }

            // BÚSQUEDA INTELIGENTE de nuevos colonos
            var newCandidates = GetChatEligibleColonistsFromCenter(center, map, group)
            .Where(p => !KickedOut.Contains(p)) // ✅ NO reintegrar automáticamente
            .ToList();


            // Tu código existente de participationTracker y el resto...
            var participationTracker = new Dictionary<Pawn, int>();
            var lastSpeaker = (Pawn)null;
            var mentionedColonists = new HashSet<Pawn>();
            bool isFirstPlayerMessage = session.History.Count(h => h.StartsWith("You:")) == 1;
            
            foreach (var p in group)
            {
                participationTracker[p] = 0;
            }

            // NUEVOS COLONOS más contextuales
            foreach (var newColono in newCandidates)
            {
                group.Add(newColono);
                participationTracker[newColono] = 0;
                
                Room newColonoRoom = newColono.Position.GetRoom(map);
                Room centerRoom = center.GetRoom(map);
                
                string joinMessage;
                if (newColonoRoom != null && centerRoom != null && newColonoRoom == centerRoom)
                {
                    joinMessage = "EchoColony.ColonistJoinsRoom".Translate(newColono.LabelShort);
                }
                else if (newColonoRoom == null && centerRoom == null)
                {
                    joinMessage = "EchoColony.ColonistJoinsOutdoor".Translate(newColono.LabelShort);
                }
                else
                {
                    joinMessage = "EchoColony.ColonistJoinsNearby".Translate(newColono.LabelShort);
                }
                
                session.AddMessage(joinMessage);

                string welcomePrompt = BuildWelcomePrompt(newColono, group, message, isFirstPlayerMessage);

                yield return ProcessSpeakerTurn(newColono, welcomePrompt, (response) => {
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        session.AddMessage(newColono.LabelShort + ": " + response);
                        participationTracker[newColono] = 1;
                        lastSpeaker = newColono;
                    }
                });
            }

            // ✅ SISTEMA DE TURNOS NATURALES
            const int MAX_TURNS_PER_COLONIST = 2;
            const int MAX_TOTAL_TURNS = 8;
            
            int totalTurns = 0;
            int safetyCounter = 0;
            const int maxSafetyTurns = 15;

            while (totalTurns < MAX_TOTAL_TURNS && safetyCounter < maxSafetyTurns)
            {
                safetyCounter++;
                
                // ✅ NUEVO: Prioridad inteligente de turnos
                Pawn nextSpeaker = DetermineNextSpeaker(group, participationTracker, lastSpeaker, mentionedColonists);
                
                if (nextSpeaker == null) break;

                // ✅ Contexto más inteligente
                var recentHistory = session.History
                .Where(line => !line.StartsWith("EchoColony.ColonistJoins") && 
                  !line.StartsWith("EchoColony.ColonistNoLongerPresent") && 
                  !line.StartsWith("EchoColony.AllColonistsGone") &&
                  !line.StartsWith("EchoColony.ColonistJoinsManually") &&
                  !line.StartsWith("EchoColony.ColonistLeavesManually") &&
                  !line.StartsWith("EchoColony.ChatCleared") &&
                  !line.StartsWith("[DATE_SEPARATOR]"))
                    .TakeLast(6)
                    .ToList();

                bool isFirstTurn = (totalTurns == 0);
                var prompt = BuildIntelligentGroupPrompt(nextSpeaker, group, recentHistory, message, isFirstTurn, isFirstPlayerMessage);

                // Indicador temporal
                session.AddMessage(nextSpeaker.LabelShort + ": ...");
                yield return new WaitForSecondsRealtime(0.3f);

                bool turnCompleted = false;
                yield return ProcessSpeakerTurn(nextSpeaker, prompt, (response) => {
                    // Remover indicador
                    if (session.History.Count > 0 && session.History.Last().EndsWith(": ..."))
                        session.History.RemoveAt(session.History.Count - 1);

                    if (!string.IsNullOrWhiteSpace(response) && response.Trim().Length >= 4)
                    {
                        session.AddMessage(nextSpeaker.LabelShort + ": " + response);
                        participationTracker[nextSpeaker]++;
                        lastSpeaker = nextSpeaker;
                        totalTurns++;
                        
                        // ✅ NUEVO: Detectar menciones en la respuesta
                        DetectMentions(response, group, mentionedColonists);
                        
                        // Micro-interacciones ocasionales
                        if (Rand.Chance(0.15f) && group.Count > 2 && totalTurns < MAX_TOTAL_TURNS - 2)
                        {
                            MyStoryModComponent.Instance.StartCoroutine(AddQuickReaction(group, nextSpeaker));
                        }
                    }
                    turnCompleted = true;
                });

                while (!turnCompleted) yield return null;

                yield return new WaitForSecondsRealtime(1.0f);
                
                // Salida temprana si todos participaron
                if (participationTracker.Values.All(turns => turns >= 1) && totalTurns >= group.Count)
                {
                    break;
                }
            }

            yield return SaveConversationSummary(group);
        }

        // ✅ NUEVO: Determinar siguiente speaker de forma inteligente
        private Pawn DetermineNextSpeaker(List<Pawn> group, Dictionary<Pawn, int> participationTracker, 
                                         Pawn lastSpeaker, HashSet<Pawn> mentionedColonists)
        {
            const int MAX_TURNS_PER_COLONIST = 2;
            
            // 1. ✅ PRIORIDAD MÁXIMA: Colonos mencionados que no han respondido
            var mentionedAndAvailable = mentionedColonists
                .Where(p => group.Contains(p) && 
                           participationTracker[p] < MAX_TURNS_PER_COLONIST && 
                           p != lastSpeaker)
                .ToList();
            
            if (mentionedAndAvailable.Any())
            {
                var chosen = mentionedAndAvailable.First();
                mentionedColonists.Remove(chosen); // Limpiar la mención una vez procesada
                return chosen;
            }

            // 2. ✅ PRIORIDAD ALTA: Quien no ha hablado todavía
            var neverSpoken = group
                .Where(p => participationTracker[p] == 0 && p != lastSpeaker)
                .ToList();
            
            if (neverSpoken.Any())
            {
                return neverSpoken.OrderBy(p => Rand.Value).First();
            }

            // 3. ✅ PRIORIDAD NORMAL: Quien menos ha hablado
            var availableSpeakers = group
                .Where(p => participationTracker[p] < MAX_TURNS_PER_COLONIST && p != lastSpeaker)
                .OrderBy(p => participationTracker[p])
                .ThenBy(p => Rand.Value)
                .ToList();

            if (availableSpeakers.Any())
            {
                return availableSpeakers.First();
            }

            // 4. ✅ ÚLTIMO RECURSO: Una ronda más evitando el último speaker
            var finalOptions = group
                .Where(p => p != lastSpeaker)
                .OrderBy(p => participationTracker[p])
                .Take(1)
                .ToList();

            return finalOptions.FirstOrDefault();
        }

        // ✅ NUEVO: Detectar menciones de otros colonos
        private void DetectMentions(string response, List<Pawn> group, HashSet<Pawn> mentionedColonists)
        {
            string responseLower = response.ToLower();
            
            foreach (var colono in group)
            {
                string name = colono.LabelShort.ToLower();
                
                // Buscar menciones directas o preguntas dirigidas
                if (responseLower.Contains(name) || 
                    responseLower.Contains($"qué opinas, {name}") ||
                    responseLower.Contains($"what do you think, {name}") ||
                    responseLower.Contains($"{name}, ") ||
                    responseLower.Contains($"@{name}") ||
                    responseLower.Contains($"¿{name}?") ||
                    responseLower.Contains($"{name}?"))
                {
                    mentionedColonists.Add(colono);
                }
            }
        }

        // ✅ NUEVO: Prompt para nuevos colonos más contextual
        private string BuildWelcomePrompt(Pawn newColono, List<Pawn> group, string originalMessage, bool isFirstPlayerMessage)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"You are {newColono.LabelShort}. You just arrived near {string.Join(", ", group.Where(p => p != newColono).Select(p => p.LabelShort))}.");
            
            if (isFirstPlayerMessage)
            {
                // Si es la primera conversación, el jugador acaba de hablar
                sb.AppendLine($"The player just said: '{originalMessage}'");
                sb.AppendLine("You heard this and want to join the conversation.");
                sb.AppendLine("Respond naturally to what the player said, as if you just walked up and heard it.");
                sb.AppendLine("Don't ask 'what are you talking about' - you heard what was said.");
            }
            else
            {
                // Si ya hay historial, la conversación está en curso
                sb.AppendLine("You notice them talking and want to join in.");
                sb.AppendLine("Say a brief greeting or politely ask what's being discussed.");
            }
            
            sb.AppendLine("Keep it natural and short (1-2 sentences).");
            
            return sb.ToString();
        }

        // ✅ MEJORADO: Prompt más inteligente y contextual
        // ✅ MEJORADO: Prompt más inteligente y contextual
private string BuildIntelligentGroupPrompt(Pawn speaker, List<Pawn> group, List<string> recentHistory, 
                                         string userMessage, bool isFirstTurn, bool isFirstPlayerMessage)
{
    // ✅ USAR el nuevo GroupPromptContextBuilder en lugar del prompt simple
    return GroupPromptContextBuilder.Build(speaker, group, recentHistory, userMessage, isFirstTurn);
}

// ✅ OPCIONAL: Si quieres mantener compatibilidad, puedes añadir también este método helper:
private string BuildContextualPrompt(Pawn speaker, List<Pawn> group, List<string> recentHistory, 
                                   string userMessage, bool isFirstResponse)
{
    return GroupPromptContextBuilder.Build(speaker, group, recentHistory, userMessage, isFirstResponse);
}

        private IEnumerator AddQuickReaction(List<Pawn> group, Pawn lastSpeaker)
        {
            var reactor = group.Where(p => p != lastSpeaker).RandomElementWithFallback();
            if (reactor == null) yield break;

            // Prompt ultra-corto para reacciones rápidas
            string prompt = "Quick reaction to what was just said. Very brief - just a few words or short sentence.";

            yield return ProcessSpeakerTurn(reactor, prompt, (response) => {
                if (!string.IsNullOrWhiteSpace(response) && response.Length < 100)
                {
                    session.AddMessage($"{reactor.LabelShort}: {response}");
                }
            });
        }

        // ✅ MÉTODO AUXILIAR: Procesar turno de un speaker de forma más eficiente
        private IEnumerator ProcessSpeakerTurn(Pawn speaker, string prompt, System.Action<string> onComplete)
{
    string response = "";
    bool complete = false;

    System.Action<string> callback = (result) =>
    {
        response = result;
        complete = true;
    };

    // ✅ NUEVO: Detectar modelo y usar el método correcto
    bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                    MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

    bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                      MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

    string finalPrompt;
    IEnumerator coroutine;

    if (isKobold)
    {
        finalPrompt = KoboldPromptBuilder.Build(speaker, prompt);
        coroutine = GeminiAPI.SendRequestToLocalModel(finalPrompt, callback);
    }
    else if (isLMStudio)
    {
        finalPrompt = LMStudioPromptBuilder.Build(speaker, prompt);
        coroutine = GeminiAPI.SendRequestToLocalModel(finalPrompt, callback);
    }
    else if (MyMod.Settings.modelSource == ModelSource.Local)
    {
        coroutine = GeminiAPI.SendRequestToLocalModel(prompt, callback);
    }
    else if (MyMod.Settings.modelSource == ModelSource.Player2)
    {
        coroutine = GeminiAPI.SendRequestToPlayer2(speaker, prompt, callback);
    }
    else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
    {
        // Para OpenRouter, necesitarías adaptar el prompt al formato JSON si es necesario
        coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, callback);
    }
    else // Gemini (por defecto)
    {
        coroutine = GeminiAPI.SendRequestToGemini(prompt, callback);
    }

    yield return coroutine;

    // ✅ TIMEOUT MÁS CORTO para evitar esperas largas
    int waitCounter = 0;
    const int maxWaitTime = 300; // 5 segundos máximo

    while (!complete && waitCounter < maxWaitTime)
    {
        yield return null;
        waitCounter++;
    }

    if (waitCounter >= maxWaitTime)
    {
        Log.Warning($"[EchoColony] Timeout en respuesta de {speaker.LabelShort}");
        response = ""; // Respuesta vacía por timeout
    }

    onComplete?.Invoke(response);
}

        // ✅ NUEVO MÉTODO: Guardar memoria unificada por colono
        private void SaveUnifiedMemories(List<Pawn> group, string resumenFinal)
        {
            if (MyStoryModComponent.Instance?.ColonistMemoryManager == null) return;

            int today = GenDate.DaysPassed;

            // Iniciar proceso de memorias personalizadas
            MyStoryModComponent.Instance.StartCoroutine(SavePersonalizedMemories(group, resumenFinal, today));
        }

        private IEnumerator SavePersonalizedMemories(List<Pawn> group, string conversationSummary, int today)
        {
            // ✅ MENSAJE SUTIL DEL SISTEMA en lugar del chat
            Messages.Message("EchoColony.SavingMemories".Translate(), MessageTypeDefOf.SilentInput, false);

            foreach (var pawn in group)
            {
                var tracker = MyStoryModComponent.Instance.ColonistMemoryManager.GetTrackerFor(pawn);
                if (tracker == null) continue;

                // Lista de otros participantes (excluyendo al pawn actual)
                string otherParticipants = string.Join(", ", group.Where(p => p != pawn).Select(p => p.LabelShort));

                // Crear prompt personalizado muy específico
                string personalPrompt = BuildPersonalMemoryPrompt(pawn, otherParticipants, conversationSummary);

                string personalMemory = "";
                bool memoryComplete = false;
                bool memoryError = false;

                // Callback para manejar la respuesta
                System.Action<string> memoryCallback = (response) =>
                {
                    if (!string.IsNullOrWhiteSpace(response) &&
                        !response.StartsWith("❌") &&
                        !response.StartsWith("⚠️") &&
                        response.Length > 10) // Asegurar que no sea muy corta
                    {
                        personalMemory = response.Trim();

                        // Limpiar cualquier formato no deseado
                        personalMemory = CleanPersonalMemory(personalMemory);
                    }
                    else
                    {
                        memoryError = true;
                        // Fallback con información básica
                        personalMemory = $"Tuve una conversación grupal con {otherParticipants}. {conversationSummary}";
                    }
                    memoryComplete = true;
                };

                // Enviar request para generar memoria personalizada
                bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

                bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                                MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

                IEnumerator memoryCoroutine;

                if (isKobold)
                {
                    string koboldPrompt = KoboldPromptBuilder.Build(pawn, personalPrompt);
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, memoryCallback);
                }
                else if (isLMStudio)
                {
                    string lmPrompt = LMStudioPromptBuilder.Build(pawn, personalPrompt);
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, memoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Local)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToLocalModel(personalPrompt, memoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Player2)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToPlayer2(pawn, personalPrompt, memoryCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToOpenRouter(personalPrompt, memoryCallback);
                }
                else // Gemini (por defecto)
                {
                    memoryCoroutine = GeminiAPI.SendRequestToGemini(personalPrompt, memoryCallback);
                }

                yield return memoryCoroutine;

                // Esperar hasta que la memoria esté lista
                int waitCounter = 0;
                while (!memoryComplete && waitCounter < 600) // Timeout de 10 segundos
                {
                    yield return null;
                    waitCounter++;
                }

                if (waitCounter >= 600)
                {
                    // Timeout - usar memoria genérica
                    personalMemory = $"Participé en una conversación grupal con {otherParticipants}. {conversationSummary}";
                }

                // Guardar memoria personalizada con formato estándar
                string finalMemory = $"[Conversación grupal con {otherParticipants}]\n{personalMemory}";
                tracker.SaveMemoryForDay(today, finalMemory);

                // Pequeña pausa entre colonos para no saturar la API
                yield return new WaitForSecondsRealtime(0.8f);
            }

            // ✅ MENSAJE SUTIL DE CONFIRMACIÓN
            Messages.Message("EchoColony.MemoriesSaved".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        private string BuildPersonalMemoryPrompt(Pawn pawn, string otherParticipants, string conversationSummary)
        {
            var sb = new System.Text.StringBuilder();

            // Contexto básico del colono
            sb.AppendLine($"You are {pawn.LabelShort}. You just finished a group conversation.");
            sb.AppendLine($"Other participants: {otherParticipants}");
            sb.AppendLine();

            // Personalidad básica para influir en la perspectiva
            if (pawn.story?.traits != null)
            {
                var relevantTraits = pawn.story.traits.allTraits
                    .Where(t => t.def.defName.ToLower().Contains("kind") ||
                               t.def.defName.ToLower().Contains("abrasive") ||
                               t.def.defName.ToLower().Contains("optimist") ||
                               t.def.defName.ToLower().Contains("pessimist") ||
                               t.def.defName.ToLower().Contains("neurotic") ||
                               t.def.defName.ToLower().Contains("psychopath"))
                    .ToList();

                if (relevantTraits.Any())
                {
                    sb.AppendLine($"Your personality traits: {string.Join(", ", relevantTraits.Select(t => t.LabelCap))}");
                }
            }

            // Estado emocional actual
            float mood = pawn.needs?.mood?.CurInstantLevel ?? 0.5f;
            string moodDesc = mood >= 0.7f ? "good mood" : mood >= 0.4f ? "okay" : "bad mood";
            sb.AppendLine($"You are currently in a {moodDesc}.");
            sb.AppendLine();

            // El resumen de la conversación
            sb.AppendLine("Here's what happened in the conversation:");
            sb.AppendLine(conversationSummary);
            sb.AppendLine();

            // Instrucciones específicas para generar memoria personal
            sb.AppendLine("Write a brief personal memory of this conversation from YOUR perspective only.");
            sb.AppendLine("Focus on:");
            sb.AppendLine("- How YOU felt about what was discussed");
            sb.AppendLine("- What YOU thought was important");
            sb.AppendLine("- YOUR personal reactions or concerns");
            sb.AppendLine("- Any specific interactions YOU had with the others");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Write in first person (I, me, my)");
            sb.AppendLine("- Keep it under 100 words");
            sb.AppendLine("- Be authentic to your personality");
            sb.AppendLine("- Don't just repeat the summary - add your personal perspective");
            sb.AppendLine("- Write in natural, conversational language");

            return sb.ToString();
        }

        // ✅ AGREGAR este método para limpiar la memoria:
        private string CleanPersonalMemory(string memory)
        {
            if (string.IsNullOrWhiteSpace(memory)) return memory;

            // Remover comillas si toda la memoria está entre comillas
            memory = memory.Trim();
            if (memory.StartsWith("\"") && memory.EndsWith("\""))
            {
                memory = memory.Substring(1, memory.Length - 2).Trim();
            }

            // Remover prefijos comunes que a veces agrega la IA
            string[] prefixesToRemove = {
        "Personal memory:",
        "My memory:",
        "Memory:",
        "I remember:",
        "Here's my memory:"
    };

            foreach (var prefix in prefixesToRemove)
            {
                if (memory.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    memory = memory.Substring(prefix.Length).Trim();
                    break;
                }
            }

            // Asegurar que empiece con mayúscula
            if (memory.Length > 0)
            {
                memory = char.ToUpper(memory[0]) + memory.Substring(1);
            }

            return memory;
        }

        // ✅ MÉTODO SEPARADO PARA EL RESUMEN (más limpio)
        private IEnumerator SaveConversationSummary(List<Pawn> group)
        {
            string fullSummary = string.Join("\n", session.History.Where(m => !m.StartsWith("EchoColony.ColonistJoins") && !m.StartsWith("EchoColony.ColonistNoLongerPresent") && !m.StartsWith("You:")));

            if (!string.IsNullOrWhiteSpace(fullSummary))
            {
                // Prompt más corto para resumen
                string resumenPrompt = $"Briefly summarize this conversation in 2-3 sentences:\n\n{fullSummary}";
                string resumenFinal = fullSummary;

                bool resumenComplete = false;
                System.Action<string> resumenCallback = (response) => {
                    resumenFinal = !string.IsNullOrWhiteSpace(response) && !response.StartsWith("❌") 
                        ? response.Trim() 
                        : fullSummary;
                    resumenComplete = true;
                };

                bool isKobold = MyMod.Settings.modelSource == ModelSource.Local &&
                MyMod.Settings.localModelProvider == LocalModelProvider.KoboldAI;

                bool isLMStudio = MyMod.Settings.modelSource == ModelSource.Local &&
                                MyMod.Settings.localModelProvider == LocalModelProvider.LMStudio;

                IEnumerator resumenCoroutine;

                if (isKobold)
                {
                    string koboldPrompt = KoboldPromptBuilder.Build(group[0], resumenPrompt);
                    resumenCoroutine = GeminiAPI.SendRequestToLocalModel(koboldPrompt, resumenCallback);
                }
                else if (isLMStudio)
                {
                    string lmPrompt = LMStudioPromptBuilder.Build(group[0], resumenPrompt);
                    resumenCoroutine = GeminiAPI.SendRequestToLocalModel(lmPrompt, resumenCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Local)
                {
                    resumenCoroutine = GeminiAPI.SendRequestToLocalModel(resumenPrompt, resumenCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.Player2)
                {
                    resumenCoroutine = GeminiAPI.SendRequestToPlayer2(group[0], resumenPrompt, resumenCallback);
                }
                else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
                {
                    resumenCoroutine = GeminiAPI.SendRequestToOpenRouter(resumenPrompt, resumenCallback);
                }
                else // Gemini (por defecto)
                {
                    resumenCoroutine = GeminiAPI.SendRequestToGemini(resumenPrompt, resumenCallback);
                }

                yield return resumenCoroutine;
                
                int waitCounter = 0;
                while (!resumenComplete && waitCounter < 200)
                {
                    yield return null;
                    waitCounter++;
                }

                SaveUnifiedMemories(group, resumenFinal);
            }
        }

        private string GetDisplayMessage(string msg)
{
    if (msg.StartsWith("[DATE_SEPARATOR]"))
        return msg.Substring("[DATE_SEPARATOR]".Length).Trim();
    else if (msg.StartsWith("EchoColony.ColonistJoins") || 
             msg.StartsWith("EchoColony.ColonistNoLongerPresent") || 
             msg.StartsWith("EchoColony.AllColonistsGone") ||
             msg.StartsWith("EchoColony.ColonistJoinsManually") ||    // ✅ NUEVO
             msg.StartsWith("EchoColony.ColonistLeavesManually") ||   // ✅ NUEVO
             msg.StartsWith("EchoColony.ChatCleared"))                // ✅ NUEVO
        return msg; // Estos ya están traducidos
    else
        return msg;
}

        private void DrawDateSeparator(Rect rect, string msg)
        {
            string dateText = msg.Substring("[DATE_SEPARATOR]".Length).Trim();
            
            // Fondo sutil para el separador  
            Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.4f, 0.5f, 0.3f));
            
            // Texto centrado con estilo especial
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            Widgets.Label(rect, dateText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

    }
}