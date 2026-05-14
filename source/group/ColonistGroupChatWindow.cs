using UnityEngine;
using Verse;
using System.Collections.Generic;
using Verse.Sound;				
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
        private List<Pawn>  participants;
        private GroupChatSession session;
        private Vector2     scrollPos             = Vector2.zero;
        private Vector2     participantsScrollPos = Vector2.zero;
        private string      userMessage           = "";
        private bool        sendRequestedViaEnter = false;
        private bool        showParticipantManagement = false;
        private List<float> cachedHeights = new List<float>();
        private string lastMessageTextSeen = null; //*furel - hight calculation sistem* New variable for control.												  
        private Vector2 portraitsScrollPos = Vector2.zero;
        //private HashSet<Pawn> KickedOut => session.KickedOutColonists; *furel - Deleted Kickout list*
        private Pawn          initiator;

        private List<Pawn> availableColonists = new List<Pawn>();
		
//*furel - Mode group formation* New variable for tracking the whay groups are made.
        public enum GroupSelectionMode
        {
            Room,       // Near colonist
            Area,       // Area of 15 squares
            MapWide     // All map
        }

        // Current mode (Room by default)
        private GroupSelectionMode currentMode = GroupSelectionMode.Room;
        // Per-colonist color palette — distinct, readable on dark backgrounds
        private static readonly Color[] PawnColors = new Color[]
        {
            new Color(0.55f, 0.85f, 1.00f),   // sky blue
            new Color(0.75f, 1.00f, 0.60f),   // soft green
            new Color(1.00f, 0.80f, 0.40f),   // warm amber
            new Color(0.90f, 0.55f, 1.00f),   // lavender
            new Color(1.00f, 0.60f, 0.60f),   // soft red / salmon
            new Color(0.40f, 1.00f, 0.90f),   // cyan-teal
            new Color(1.00f, 0.90f, 0.40f),   // yellow
            new Color(0.80f, 0.60f, 1.00f),   // purple
            new Color(0.55f, 1.00f, 0.75f),   // mint
            new Color(1.00f, 0.70f, 0.30f),   // orange
        };

        // Maps each pawn to a stable color index based on their ThingID hash
        private Dictionary<string, Color> pawnColorCache = new Dictionary<string, Color>();

        private const float MAX_CHAT_DISTANCE     = 15f;
        private const float OUTDOOR_CHAT_DISTANCE = 8f;

        public ColonistGroupChatWindow(List<Pawn> participants, GroupSelectionMode initialMode) //*furel - Mode group formation* Added initialMode as variable imput.
        {
            this.initiator = participants.First();
            this.currentMode = initialMode; //*furel - Mode group formation* 
            this.session = GroupChatGameComponent.Instance.GetOrCreateSession(participants);
		
		//*furel - Deleted Kickout list*
        //    if (this.session.KickedOutColonists == null)
        //        this.session.KickedOutColonists = new HashSet<Pawn>();

            // this.participants = participants
                // .Distinct()
                // .Where(p => !this.session.KickedOutColonists.Contains(p))
                // .ToList();
				
			this.participants = participants.Distinct().ToList();
			
            if (this.participants.Count == 0 || !this.participants.Contains(this.initiator))
            {
                this.participants.Clear();
                this.participants.Add(this.initiator);
                //this.session.KickedOutColonists.Remove(this.initiator); *furel - Deleted Kickout list*
            }

            this.session = GroupChatGameComponent.Instance
                .UpdateSessionParticipants(this.session, this.participants);

            BuildColorCache(this.participants);

            this.doCloseX               = true;
            this.forcePause             = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside  = true;
            this.closeOnAccept          = false;
        }

        // ── Color helpers ────────────────────────────────────────────────────────

        // Assigns a stable color to each pawn based on their ThingID.
        // Hash-based assignment means colors don't shift when participants change.
        private void BuildColorCache(List<Pawn> pawns)
        {
            pawnColorCache.Clear();
            int colorIndex = 0;
            foreach (var p in pawns)
            {
                // Use ThingID hash to pick a stable slot, then walk through palette
                // avoiding collisions with already-assigned colors
                int hash    = Math.Abs(p.ThingID.GetHashCode());
                int startAt = hash % PawnColors.Length;
                int attempts = 0;

                while (pawnColorCache.Values.Any(c => c == PawnColors[startAt]) &&
                       attempts < PawnColors.Length)
                {
                    startAt = (startAt + 1) % PawnColors.Length;
                    attempts++;
                }

                pawnColorCache[p.LabelShort] = PawnColors[startAt];
                colorIndex++;
            }
        }

        // Returns the assigned color for a pawn name, or white as fallback
        private Color GetPawnColor(string pawnName)
        {
            if (pawnColorCache.TryGetValue(pawnName, out var color))
                return color;
            return new Color(1f, 0.95f, 0.85f);
        }

        // Adds a new participant's color if they weren't in the original group
        private void EnsureColorFor(Pawn pawn)
        {
            if (pawnColorCache.ContainsKey(pawn.LabelShort)) return;

            int hash    = Math.Abs(pawn.ThingID.GetHashCode());
            int startAt = hash % PawnColors.Length;
            int attempts = 0;

            while (pawnColorCache.Values.Any(c => c == PawnColors[startAt]) &&
                   attempts < PawnColors.Length)
            {
                startAt = (startAt + 1) % PawnColors.Length;
                attempts++;
            }

            pawnColorCache[pawn.LabelShort] = PawnColors[startAt];
        }

        public override Vector2 InitialSize => new Vector2(1100f, 700f);

        // ── Main draw ────────────────────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            float y = 10f;
            DrawHeader(inRect, ref y);

            if (showParticipantManagement)
                DrawParticipantManagement(inRect, ref y);

            DrawChatArea(inRect, ref y);
            DrawBottomControls(inRect, y);
        }

        // ── Header ───────────────────────────────────────────────────────────────

        private void DrawHeader(Rect inRect, ref float y)
        {
            float btnW = 70f;
            float gap  = 5f;

            if (Widgets.ButtonText(new Rect(10f, y, btnW, 25f), "EchoColony.GCWExpotB".Translate())) //*furel - button translation*
                ExportChat();

            GUI.color = new Color(1f, 0.7f, 0.7f);
            if (Widgets.ButtonText(new Rect(10f + btnW + gap, y, btnW, 25f), "EchoColony.GCWClearB".Translate())) //*furel - button translation*
                ClearChat();
            GUI.color = Color.white;

            Text.Font = GameFont.Medium;
            string names  = string.Join(", ", participants.Select(p => p.LabelShort));
            float  titleX = 10f + btnW * 2 + gap * 2 + 10f;
            Widgets.Label(new Rect(titleX, y, inRect.width - titleX - 150f, 30f),
                $"{"EchoColony.GroupChatLabel".Translate()} {names}");
            Text.Font = GameFont.Small;

            Rect memberBtn = new Rect(inRect.width - 140f, y, 130f, 25f);
            if (Widgets.ButtonText(memberBtn,
                showParticipantManagement ? "EchoColony.GCWHideMemB".Translate() : "EchoColony.GCWManegeMemB".Translate())) //*furel - button translation*
            {
                showParticipantManagement = !showParticipantManagement;
                if (showParticipantManagement)
                    UpdateAvailableColonists();
            }

            y += 35f;
            DrawPortraits(inRect, ref y); //y=45
        }


        private void DrawPortraits(Rect inRect, ref float y)
        {
            float size    = 60f;
            float spacing = 15f;
            float rowHeight = size + 25f;

            Rect scrollRect = new Rect(10f, y, inRect.width - 20f, rowHeight + 16f);

            float totalContentWidth = participants.Count * (size + spacing);

            float viewWidth = Mathf.Max(totalContentWidth, scrollRect.width - 16f);
            Rect viewRect = new Rect(0, 0, viewWidth, rowHeight);

            float startX = 0f;
            if (totalContentWidth < scrollRect.width)
            {
                startX = (scrollRect.width - totalContentWidth) / 2f;
            }

            Widgets.BeginScrollView(scrollRect, ref portraitsScrollPos, viewRect);

            for (int i = 0; i < participants.Count; i++)
            {
                var   p = participants[i];
                float x = startX + i * (size + spacing);

                // Portrait with a subtle color border matching the pawn's chat color
                Color pawnCol = GetPawnColor(p.LabelShort);
                GUI.color     = new Color(pawnCol.r, pawnCol.g, pawnCol.b, 0.6f);
                GUI.DrawTexture(new Rect(x - 2f, 0, size + 4f, size + 4f),
                    BaseContent.WhiteTex);
                GUI.color = Color.white;

                GUI.DrawTexture(new Rect(x, 2f, size, size),
                    PortraitsCache.Get(p, new Vector2(size, size), Rot4.South, default, 1.25f));

                // Name in pawn color
                GUI.color = pawnCol;
                Widgets.Label(new Rect(x, size + 4f, size + 40f, 35f), p.LabelShort);
                GUI.color = Color.white;

                if (participants.Count >= 3 && p != initiator)
                {
                    Rect removeBtn = new Rect(x + size - 15f, 0, 20f, 20f);
                    GUI.color = Color.red;
                    if (Widgets.ButtonText(removeBtn, "×"))
                        RemoveParticipant(p);
                    GUI.color = Color.white;
                }
            }
            Widgets.EndScrollView();

            y += scrollRect.height + 10f;
        }

		//*furel - Mode group formation* Changed how the management panel is drawn, `pliting the space in this call and creating two separated calls to draw their respective spaces 
        // ── Participant management panel ─────────────────────────────────────────

        private void DrawParticipantManagement(Rect inRect, ref float y)
        {  
            float splitX = inRect.width * 0.15f;
            Rect panelModes = new Rect(inRect.x + 5f, y, splitX - 10f, 160f);
            Rect panelList = new Rect(inRect.x + splitX + 5f, y, inRect.width - splitX - 15f, 160f);

            DrawModeSelectionMenu(panelModes); //*furel - Mode group formation* Call for Mode selesction menu.

            DrawAvailableColonistsList(panelList); //*furel - Mode group formation* Call for pawns in range menu.

            y += panelList.height + 10f;
        }

        //*furel - Mode group formation* Separated function to draw mode menu.
		private void DrawModeSelectionMenu(Rect rect)
        {
            float btnHeight = 24f;
            float spacing = 5f;


            // Title
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "EchoColony.GCWSelectMode".Translate());

            var modes = new[] 
            {
                (GroupSelectionMode.Room, "EchoColony.GCWModeRoom"),
                (GroupSelectionMode.Area, "EchoColony.GCWModeArea"),
                (GroupSelectionMode.MapWide, "EchoColony.GCWModeMap")
            };

            float currentY = rect.y + 25f;
            
            foreach (var mode in modes)
            {
                Rect r = new Rect(rect.x, currentY, rect.width, btnHeight);
                bool isSelected = (currentMode == mode.Item1);

                // Widgets.RadioButtonLabeled draws the circle and the leabel 
                 //returns true when is selected.
                if (Widgets.RadioButtonLabeled(r, mode.Item2.Translate(), isSelected))
                {
                    if (!isSelected)
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        currentMode = mode.Item1;

                        // Update the list on mode selection 
                        UpdateAvailableColonists();
                    }
                }

                currentY += btnHeight + spacing;
            }
        }
		//*furel - Mode group formation* Separated function to draw the avariable colonist, deleted Kickout 
		private void DrawAvailableColonistsList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.xMax - 10f, 25f),
                "EchoColony.GCWManagerColonNear".Translate(availableColonists.Count));
				
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, 200f, 25f),
            "EchoColony.GCWManagerColonNear".Translate(availableColonists.Count));
			
            Rect scrollRect = new Rect(rect.x + 10f, rect.y + 30f, rect.width - 10f, 120f);
            float rowH = 25f;
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16f, availableColonists.Count * rowH);																		   
            Widgets.BeginScrollView(scrollRect, ref participantsScrollPos, viewRect);

            for (int i = 0; i < availableColonists.Count; i++)
            {
                Pawn colonist = availableColonists[i];
                Rect rowRect = new Rect(0, i * rowH, viewRect.width, rowH - 2f);

                // Backgroud line
                if (i % 2 == 0) Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));

                // Icon and Name					  
                Widgets.Label(new Rect(rowRect.x + 5f, rowRect.y, 130f, rowRect.height), colonist.LabelShort);
                float dist = (colonist.Position - initiator.Position).LengthHorizontal;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rowRect.x + 150f, rowRect.y, 60f, rowRect.height), $"{dist:F1}m");
                GUI.color = Color.white;

                // ADD Button
                Rect addBtn = new Rect(rowRect.x + rowRect.width - 85f, rowRect.y + 2f, 80f, 20f);
                if (Widgets.ButtonText(addBtn, "EchoColony.GCWAdd".Translate()))
                {
                    AddParticipant(colonist);
                    UpdateAvailableColonists();
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }						
            }
            Widgets.EndScrollView();
        }
		
        // ── Chat area ────────────────────────────────────────────────────────────

        private void DrawChatArea(Rect inRect, ref float y)
        {
            float chatHeight = inRect.height - y - 80f;
            Rect  scrollRect = new Rect(10f, y, inRect.width - 20f, chatHeight);
            float viewW      = scrollRect.width - 16f;
//------------------------------------------------------------------------------------
            //*furel - text display* Shield the way text is displayed. If group chat window was closed and re-opened hight wasn't calculated propedly.
			if (viewW < 50f) return;						

            string currentLastMsg = session.History.Count > 0 ? session.History.Last() : null;

            if (currentLastMsg != lastMessageTextSeen || lastMessageTextSeen != currentLastMsg)
            {
                cachedHeights.Clear();

                Text.Font = GameFont.Small; 
                foreach (string m in session.History)
                {
                    cachedHeights.Add(CalculateMessageHeight(m, viewW));
                }
                lastMessageTextSeen = currentLastMsg;
            }

            if (cachedHeights.Count > session.History.Count)
            {
                cachedHeights.RemoveRange(session.History.Count, cachedHeights.Count - session.History.Count);
            }
//-------------------------------------------------------------------------------------																					  

            while (cachedHeights.Count < session.History.Count) 												
            {
                int index = cachedHeights.Count;               
                string msg = session.History[index];            
                cachedHeights.Add(CalculateMessageHeight(msg, viewW)); 						 
            }
           
            float viewH    = cachedHeights.Sum() + cachedHeights.Count * 6f; 
            Rect  viewRect = new Rect(0, 0, viewW, viewH);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            float lineY = 0;
            for (int i = 0; i < session.History.Count; i++)
            {
                string msg     = session.History[i];
                float  lineH   = cachedHeights[i]; 
                Rect   lineRect = new Rect(0, lineY, viewW, lineH);

                if (msg.StartsWith("[DATE_SEPARATOR]"))
                    DrawDateSeparator(lineRect, msg);
                else if (msg.StartsWith(GroupChatSession.SystemPrefix))
                    DrawSystemLine(lineRect, msg);
                else
                    DrawDialogueLine(lineRect, msg, viewW);

                lineY += lineH + 6f;
            }

            Widgets.EndScrollView();
            y += chatHeight + 10f;
        }

        private void DrawDateSeparator(Rect rect, string msg)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.4f, 0.5f, 0.3f));
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color   = new Color(0.8f, 0.9f, 1f, 0.9f);
            Widgets.Label(rect, GroupChatSession.GetDisplayText(msg));
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawSystemLine(Rect rect, string msg)
        {
            GUI.color   = new Color(0.7f, 0.8f, 0.7f, 0.7f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"— {GroupChatSession.GetDisplayText(msg)} —");
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Renders a dialogue line with the speaker's name in their assigned color
        // and the message body in a neutral tone.
        private void DrawDialogueLine(Rect rect, string msg, float viewW)
        {
            string text = GroupChatSession.GetDisplayText(msg);

            bool isPlayer = msg.StartsWith("You:") || msg.StartsWith("You::");

            if (isPlayer)
            {
                // Player messages: whole line in a calm blue
                GUI.color = new Color(0.8f, 0.9f, 1f);
                Widgets.Label(rect, text);
                GUI.color = Color.white;
                return;
            }

            // Colonist messages: split "Name: message" and color them separately
            int colonIdx = text.IndexOf(": ");
            if (colonIdx > 0)
            {
                string name    = text.Substring(0, colonIdx);
                string body    = text.Substring(colonIdx + 2); // skip ": "
                Color  nameCol = GetPawnColor(name);

                // We draw in two passes on the same rect:
                // Pass 1 — colored name
                string nameWithColon = name + ": ";
                float  nameW         = Text.CalcSize(nameWithColon).x;

                GUI.color = nameCol;
                Widgets.Label(new Rect(rect.x, rect.y, nameW + 2f, rect.height), nameWithColon);
                GUI.color = Color.white;

                // Pass 2 — neutral body text, offset by name width
                GUI.color = new Color(1f, 0.95f, 0.85f);
                Widgets.Label(new Rect(rect.x + nameW, rect.y, rect.width - nameW, rect.height), body);
                GUI.color = Color.white;
            }
            else
            {
                // No colon found — render as-is
                GUI.color = new Color(1f, 0.95f, 0.85f);
                Widgets.Label(rect, text);
                GUI.color = Color.white;
            }
        }

        // ── Bottom controls ──────────────────────────────────────────────────────

        private void DrawBottomControls(Rect inRect, float y)
        {
            float btnW = 80f;
            float gap  = 10f;

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return ||
                 Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "GroupChatInput" &&
                !Event.current.shift)
            {
                sendRequestedViaEnter = true;
                Event.current.Use();
            }

            Rect inputRect = new Rect(10f, y, inRect.width - btnW - gap - 20f, 60f);
            GUI.SetNextControlName("GroupChatInput");
            userMessage = GUI.TextArea(inputRect, userMessage, 500, new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                padding  = new RectOffset(6, 6, 6, 6)
            });

            Rect sendRect = new Rect(inputRect.xMax + gap, y, btnW, 60f);
            bool clicked  = Widgets.ButtonText(sendRect, "Send");

            if ((clicked || sendRequestedViaEnter) && !userMessage.NullOrEmpty())
            {
				GroupChatGameComponent.Instance.RegistingSession(session);																																																						 
                session.AddMessage("You:: " + userMessage);
                StartGroupConversation(userMessage);
                userMessage           = "";
                sendRequestedViaEnter = false;
                GUI.FocusControl(null);
                scrollPos.y = float.MaxValue;
            }
        }

        // ── Participant management ────────────────────────────────────────────────
		//*furel* Removed Kickout search
        private void AddParticipant(Pawn p)
        {
            if (p == null || participants.Contains(p)) return;
								   
            participants.Add(p);										 
            session = GroupChatGameComponent.Instance.UpdateSessionParticipants(session, participants);								 
        }

        private void RemoveParticipant(Pawn p)
        {
            if (p == initiator) return;

            if (participants.Contains(p))
            {
                participants.Remove(p);
                // Actualizamos la sesión
                session = GroupChatGameComponent.Instance.UpdateSessionParticipants(session, participants);
                UpdateAvailableColonists();
            }						 
        }
        // ── Proximity helpers ────────────────────────────────────────────────────

        private void UpdateAvailableColonists()
        {
			if (initiator == null || initiator.Map == null) return;
			List<Pawn> potentialCandidates;

            // *furel* Seleccionamos la lógica de búsqueda según el modo activo
            switch (currentMode)
            {
                case GroupSelectionMode.Room:
                    // Busca peones en la misma habitación que el iniciador[cite: 1]
                    potentialCandidates = GetPawnsInRoom(initiator);
                    break;

                case GroupSelectionMode.Area:
                    // Busca peones en un radio de 15 celdas (puedes usar la constante MAX_CHAT_DISTANCE)[cite: 1]
                    potentialCandidates = GetPawnsInArea(initiator, 15f);
                    break;

                case GroupSelectionMode.MapWide:
                default:
                    // Busca a todos los colonos y esclavos válidos en todo el mapa[cite: 1]
                    potentialCandidates = GetAllValidPawnsOnMap(initiator);
                    break;
            }

            availableColonists = potentialCandidates
                .Where(p => !participants.Contains(p))
                .ToList();
        }

       // private IntVec3 CalculateConversationCenter()
        // {
            // if (participants.Count == 0) return IntVec3.Invalid;
            // if (participants.Count == 1) return participants[0].Position;
            // return CellRect.FromLimits(
                // participants.Min(p => p.Position.x), participants.Min(p => p.Position.z),
                // participants.Max(p => p.Position.x), participants.Max(p => p.Position.z)
            // ).CenterCell;
        // }


        // private List<Pawn> GetChatEligibleColonistsFromCenter(
            // IntVec3 center, Map map, List<Pawn> exclude)
        // {
            // exclude = exclude ?? new List<Pawn>();
            // return map.mapPawns.FreeColonistsSpawned
                // .Where(p => !exclude.Contains(p) &&
                            // IsBasicEligibleFromCenter(p, center, map) &&
                            // CanCommunicateFromCenter(center, map, p))
                // .ToList();
        // }

        // private bool IsBasicEligibleFromCenter(Pawn p, IntVec3 center, Map map)
        // {
            // if (p == null || map == null || p.Dead || p.Map != map) return false;
            // if (!p.RaceProps.Humanlike || p.Faction != Faction.OfPlayer) return false;
            // return p.Position.DistanceTo(center) <= MAX_CHAT_DISTANCE;
        // }

        // private bool CanCommunicateFromCenter(IntVec3 center, Map map, Pawn colonist)
        // {
            // if (map == null || colonist?.Map != map) return false;

            // Room centerRoom   = center.GetRoom(map);
            // Room colonistRoom = colonist.Position.GetRoom(map);

            // if (centerRoom != null && colonistRoom != null && centerRoom == colonistRoom)
                // return true;

            // float dist = center.DistanceTo(colonist.Position);
            // if (dist > OUTDOOR_CHAT_DISTANCE) return false;

            // if (centerRoom == null && colonistRoom == null)
                // return GenSight.LineOfSight(center, colonist.Position, map, true);

            // return HasDirectConnection(center, colonist.Position, map);
        // }

        // private bool HasDirectConnection(IntVec3 from, IntVec3 to, Map map)
        // {
            // if (!GenSight.LineOfSight(from, to, map, true)) return false;
            // foreach (var cell in GenSight.PointsOnLineOfSight(from, to))
            // {
                // if (!cell.InBounds(map)) continue;
                // if (cell.GetDoor(map) is Building_Door door) return door.Open;
                // if (cell.Filled(map)) return false;
            // }
            // return true;
        // }

        // ── Conversation coroutine ────────────────────────────────────────────────

        private void StartGroupConversation(string message)
        {
            if (MyStoryModComponent.Instance == null) return;

            if (MyStoryModComponent.Instance.ColonistMemoryManager == null)
            {
                MyStoryModComponent.Instance.ColonistMemoryManager =
                    Current.Game.GetComponent<ColonistMemoryManager>()
                    ?? new ColonistMemoryManager(Current.Game);
                if (!Current.Game.components.Contains(
                        MyStoryModComponent.Instance.ColonistMemoryManager))
                    Current.Game.components.Add(
                        MyStoryModComponent.Instance.ColonistMemoryManager);
            }

            MyStoryModComponent.Instance.StartCoroutine(
                GroupChatCoroutine(new List<Pawn>(participants), message));
        }

        private IEnumerator GroupChatCoroutine(List<Pawn> group, string message)
        {
            Map     map    = group[0].Map;
			
			//*furel* Pawns are asigned on opening the windos. No needed with the new system.
            // IntVec3 center = CalculateCenter(group);

            //for (int i = group.Count - 1; i >= 0; i--)
            //{
            //    var p = group[i];
            //    if (p.Dead || p.Map != map)
            //    {
            //        session.AddSystemMessage($"{p.LabelShort} is no longer present");
            //        group.RemoveAt(i);
            //    }
            //    else if (!CanCommunicateFromCenter(center, map, p))
            //   {
            //        session.AddSystemMessage($"{p.LabelShort} moved too far away");
            //        group.RemoveAt(i);
            //    }
            //}

            if (group.Count == 0)
            {
                session.AddSystemMessage("No one is left to talk to");
                yield break;
            }

            //center = CalculateCenter(group); *furel* no needed with the new system.

            var participationCount = new Dictionary<Pawn, int>();
            foreach (var p in group) participationCount[p] = 0;

            var  mentionedPawns = new HashSet<Pawn>();
            Pawn lastSpeaker    = null;

            int totalTurns = 0;
            int maxTurns   = Mathf.Clamp(group.Count * 2, 4, 10);
            int safety     = 0;
            const int maxSafety = 20;

            while (totalTurns < maxTurns && safety < maxSafety)
            {
                safety++;

                Pawn next = PickNextSpeaker(
                    group, participationCount, lastSpeaker, mentionedPawns);
                if (next == null) break;

                bool isLateJoiner = participationCount[next] == 0 && totalTurns > 0;

                var recentHistory = session.History
                    .TakeLast(20)
                    .ToList();

                session.AddMessage(next.LabelShort + ": ...");
                yield return new WaitForSecondsRealtime(0.3f);

                string prompt = GroupPromptContextBuilder.Build(
                    next, group, recentHistory, message,
                    isFirstTurn:  totalTurns == 0,
                    isLateJoiner: isLateJoiner);

                bool done = false;
                yield return ProcessTurn(next, prompt, response =>
                {
                    // Remove placeholder
                    if (session.History.Count > 0 &&
                        session.History.Last() == next.LabelShort + ": ...")
						{
							session.History.RemoveAt(session.History.Count - 1);
							cachedHeights.RemoveAt(cachedHeights.Count - 1);
						}

                    if (!string.IsNullOrWhiteSpace(response) && response.Trim().Length >= 3)
                    {
                        string clean = StripSpeakerPrefix(response, next.LabelShort, group);

                        // ── Action processing ─────────────────────────────────────
                        // Execute [ACTION:...] tags and strip them from displayed text.
                        // This mirrors exactly what ColonistChatWindow.OnResponse does.
                        if (MyMod.Settings.enableDivineActions)
                        {
                            try
                            {
                                var processed = Actions.ActionExecutor.ProcessResponse(next, clean);
                                if (processed != null)
                                    clean = processed.CleanResponse ?? clean;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[EchoColony] Group chat action processing failed " +
                                            $"for {next.LabelShort}: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Divine Actions disabled — still strip tags so they
                            // never appear as raw text in the chat window
                            clean = System.Text.RegularExpressions.Regex.Replace(
                                clean, @"\[ACTION:[^\]]*\]", "").Trim();
                        }
                        // ─────────────────────────────────────────────────────────

                        if (!string.IsNullOrWhiteSpace(clean))
                        {
                            session.AddMessage(next.LabelShort + ": " + clean);
                            participationCount[next]++;
                            lastSpeaker = next;
                            totalTurns++;
                            DetectMentions(response, group, mentionedPawns);
                        }
                    }

                    done = true;
                });

                while (!done) yield return null;

                yield return new WaitForSecondsRealtime(0.8f);

                if (participationCount.Values.All(c => c >= 1) && totalTurns >= group.Count)
                    break;
            }

            yield return SaveMemories(group);
        }

        private string StripSpeakerPrefix(string response, string speakerName, List<Pawn> group)
        {
            if (string.IsNullOrWhiteSpace(response)) return response;

            string trimmed = response.Trim();

            string speakerPrefix = speakerName + ":";
            if (trimmed.StartsWith(speakerPrefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(speakerPrefix.Length).Trim();

            foreach (var p in group)
            {
                string otherPrefix = p.LabelShort + ":";
                if (trimmed.StartsWith(otherPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(otherPrefix.Length).Trim();
                    break;
                }
            }

            return trimmed;
        }

        // private IntVec3 CalculateCenter(List<Pawn> group)
        // {
            // if (group.Count == 0) return IntVec3.Invalid;
            // if (group.Count == 1) return group[0].Position;
            // return CellRect.FromLimits(
                // group.Min(p => p.Position.x), group.Min(p => p.Position.z),
                // group.Max(p => p.Position.x), group.Max(p => p.Position.z)
            // ).CenterCell;
        // }

        // ── Speaker selection ────────────────────────────────────────────────────

        private Pawn PickNextSpeaker(
            List<Pawn>           group,
            Dictionary<Pawn,int> counts,
            Pawn                 lastSpeaker,
            HashSet<Pawn>        mentioned)
        {
            const int maxPerPawn = 2;

            var mentionedAvailable = mentioned
                .Where(p => group.Contains(p) &&
                            counts.ContainsKey(p) &&
                            counts[p] < maxPerPawn &&
                            p != lastSpeaker)
                .ToList();

            if (mentionedAvailable.Any())
            {
                var chosen = mentionedAvailable.First();
                mentioned.Remove(chosen);
                return chosen;
            }

            var silent = group
                .Where(p => counts.ContainsKey(p) && counts[p] == 0 && p != lastSpeaker)
                .OrderBy(_ => Rand.Value)
                .ToList();

            if (silent.Any()) return silent.First();

            return group
                .Where(p => counts.ContainsKey(p) &&
                            counts[p] < maxPerPawn &&
                            p != lastSpeaker)
                .OrderBy(p => counts[p])
                .ThenBy(_ => Rand.Value)
                .FirstOrDefault();
        }

        private void DetectMentions(string response, List<Pawn> group, HashSet<Pawn> mentioned)
        {
            string lower = response.ToLower();
            foreach (var p in group)
                if (lower.Contains(p.LabelShort.ToLower()))
                    mentioned.Add(p);
        }

        // ── API call helper ──────────────────────────────────────────────────────

          private IEnumerator ProcessTurn(Pawn speaker, string prompt, Action<string> onComplete)
        {
            string result   = "";
            bool   complete = false;
            Action<string> cb = r => { result = r; complete = true; };
 
            // NOTE: For group chat, `prompt` is already the full output of
            // GroupPromptContextBuilder.Build() — it includes all pawn context,
            // colony history, conversation history, and response instructions.
            //
            // We do NOT re-wrap it through KoboldPromptBuilder or LMStudioPromptBuilder.
            // Those builders are for 1:1 direct chats only — they add pawn demographics
            // and backstory that are already present in the group prompt.
            //
            // All providers receive the prompt as a single user message.
            // The group chat format (plain text paragraph) works well for local models
            // and is much simpler than the JSON array format used by pawn conversations.
 
            IEnumerator coroutine;
 
            switch (MyMod.Settings.modelSource)
            {
                case ModelSource.Local:
                    // Send prompt directly — no re-wrapping through Kobold/LMStudio builders
                    coroutine = GeminiAPI.SendRequestToLocalModel(prompt, cb);
                    break;
 
                case ModelSource.Player2:
                    // Player2 uses its own message format; pass prompt as user content
                    coroutine = GeminiAPI.SendRequestToPlayer2WithPrompt(prompt, cb);
                    break;
 
                case ModelSource.OpenRouter:
                    coroutine = GeminiAPI.SendRequestToOpenRouter(prompt, cb);
                    break;
 
                case ModelSource.Custom:
                    coroutine = GeminiAPI.SendRequestToCustomProvider(prompt, cb);
                    break;
 
                case ModelSource.Gemini:
                default:
                    coroutine = GeminiAPI.SendRequestToGemini(prompt, cb);
                    break;
            }
 
            yield return coroutine;
 
            // Safety timeout — wait up to 300 frames (~5s at 60fps) after coroutine ends
            int waited = 0;
            while (!complete && waited < 300) { yield return null; waited++; }
 
            onComplete?.Invoke(result);
        }
 
 
        // ── Memory saving ────────────────────────────────────────────────────────

        private IEnumerator SaveMemories(List<Pawn> group)
        {
            string transcript = string.Join("\n", session.History
                .Where(l => !GroupChatSession.IsSystemMessage(l))
                .Select(GroupChatSession.GetDisplayText)
                .Where(l => !string.IsNullOrWhiteSpace(l)));

            if (string.IsNullOrWhiteSpace(transcript)) yield break;

            Messages.Message("EchoColony.SavingMemories".Translate(),
                MessageTypeDefOf.SilentInput, false);

            string summary = transcript;
            bool   sumDone = false;

            yield return ProcessTurn(group[0],
                "Summarize this group conversation in 2-3 sentences:\n\n" + transcript,
                r => { if (!string.IsNullOrWhiteSpace(r) && !r.StartsWith("⚠")) summary = r.Trim(); sumDone = true; });

            int sw = 0;
            while (!sumDone && sw < 200) { yield return null; sw++; }

            int today   = GenDate.DaysPassed;
            var manager = ColonistMemoryManager.GetOrCreate();
            if (manager == null) yield break;

            foreach (var pawn in group)
            {
                var tracker = manager.GetTrackerFor(pawn);
                if (tracker == null) continue;

                string others  = string.Join(", ", group
                    .Where(p => p != pawn).Select(p => p.LabelShort));
                string memBody = "";
                bool   memDone = false;

                yield return ProcessTurn(pawn,
                    $"You are {pawn.LabelShort}. Write a brief personal memory (under 80 words, " +
                    $"first person) of this group conversation you just had with {others}:\n\n{summary}",
                    r => { memBody = !string.IsNullOrWhiteSpace(r) ? r.Trim() : summary; memDone = true; });

                int mw = 0;
                while (!memDone && mw < 300) { yield return null; mw++; }

                tracker.SaveMemoryForDay(today,
                    $"[Group conversation with {others}]\n{memBody}");

                yield return new WaitForSecondsRealtime(0.5f);
            }

            Messages.Message("EchoColony.MemoriesSaved".Translate(),
                MessageTypeDefOf.SilentInput, false);
        }

        // ── Export / Clear ────────────────────────────────────────────────────────

        private void ExportChat()
        {
            try
            {
                string folder = Path.Combine(
                    GenFilePaths.SaveDataFolderPath, "EchoColony", "ChatExports");
                Directory.CreateDirectory(folder);

                string file = Path.Combine(folder,
                    $"GroupChat_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("=== ECHOCOLONY GROUP CHAT EXPORT ===");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Participants: {string.Join(", ", participants.Select(p => p.LabelShort))}");
                sb.AppendLine(new string('=', 42));
                sb.AppendLine();

                foreach (var msg in session.History)
                {
                    if (!GroupChatSession.IsSystemMessage(msg))
                        sb.AppendLine(GroupChatSession.GetDisplayText(msg));
                    else
                        sb.AppendLine($"  [{GroupChatSession.GetDisplayText(msg)}]");
                }

                File.WriteAllText(file, sb.ToString());
                Messages.Message($"Chat exported: {Path.GetFileName(file)}",
                    MessageTypeDefOf.TaskCompletion);

                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error exporting group chat: {ex.Message}");
                Messages.Message("Failed to export chat. Check logs.", MessageTypeDefOf.NegativeEvent);
            }
        }

        // ── Height Text Calculation ────────────────────────────────────────────────────────
		//*furel* Sometimes the text gets cut off, so I give it a little more space and trim the space used to calculate the height to force it to increase.
        private float CalculateMessageHeight(string msg, float width)
        {
			Text.Font = GameFont.Small;						   
            if (GroupChatSession.IsSystemMessage(msg)) return 24f;

			float safeWidth = width - 8f;							 
            string displayText = GroupChatSession.GetDisplayText(msg);
            if (msg.StartsWith("You:") || msg.StartsWith("You::"))
                return Text.CalcHeight(displayText, safeWidth) + 6f;

            int colonIdx = displayText.IndexOf(": ");
            if (colonIdx > 0)
            {
                string nameWithColon = displayText.Substring(0, colonIdx) + ": ";
                float nameW = Text.CalcSize(nameWithColon).x;
                string body = displayText.Substring(colonIdx + 2);
                float bodyH = Text.CalcHeight(body, safeWidth - nameW) + 6f;
                float fullH = Text.CalcHeight(displayText, safeWidth) + 6f;
                return Mathf.Max(fullH, bodyH);
            }
            return Text.CalcHeight(displayText, safeWidth) + 6f;
        }
		
        private void ClearChat()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EchoColony.GCWClearMessage".Translate(),
                () =>
                {
                    session.History.Clear();
					cachedHeights.Clear();																						  
                    session.AddSystemMessage("EchoColony.GCWClearConfirmation".Translate());
                    Messages.Message("EchoColony.GCWClearConfirmation".Translate(), MessageTypeDefOf.NeutralEvent);
                }));
        }

        //*furel* Get the list of pawns on a room.
        // ── Get Pawn Room List ────────────────────────────────────────────────────────
        public static List<Pawn> GetPawnsInRoom(Pawn initiator)
        {
            List<Pawn> results = new List<Pawn>();
            if (initiator?.Map == null) return results;

            // Check which rooms the initiator pawn is in, or if it's outside. If it's right in a doorway, take both rooms.
            var initiatorRooms = GetAllRoomsForPawn(initiator, out bool initiatorTouchesExterior);

            float exteriorRadius = 15f;
            foreach (Pawn p in initiator.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == initiator || !Patch_ChatGizmo.IsValidForGroupChat(p)) continue;

                //Check which rooms the pawn is in, or if it's outside. If it's right in a doorway, take both rooms.
                var pRooms = GetAllRoomsForPawn(p, out bool pTouchesExterior);

				//If the starter and the pawn share space, add the pawn to the list.
                if (initiatorRooms.Overlaps(pRooms) || (initiatorTouchesExterior && pTouchesExterior && p.Position.InHorDistOf(initiator.Position, exteriorRadius)))
                {
                    results.Add(p);
                }
            }
            return results;
        }

        //*furel* Get the list of pawns on an area. 15f is the default Radious
        // ── Get Pawn Area List (15 radious)────────────────────────────────────────────────────────
        public static List<Pawn> GetPawnsInArea(Pawn initiator, float radius)
        {
            List<Pawn> inArea = new List<Pawn>();

            Map map = initiator.Map;
            if (map == null) return inArea;

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p != initiator && Patch_ChatGizmo.IsValidForGroupChat(p))
                {
                    if (p.Position.InHorDistOf(initiator.Position, radius))
                    {
                        inArea.Add(p);
                    }
                }
            }
            return inArea;
        }

        //*furel* Get the list of pawns on a map.
        // ── Get Pawn Map List ────────────────────────────────────────────────────────
        private List<Pawn> GetAllValidPawnsOnMap(Pawn initiator)
        {
            return initiator.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != initiator && Patch_ChatGizmo.IsValidForGroupChat(p))
                .ToList();
        }

        //*furel* A filter for pawns between rooms or exteriors. If the pawn is right at the door separating an interior from an exterior, it takes the room it is entering or leaving.
        //─── Door check position ────────────────────────────────────────────────────────
        private static HashSet<Room> GetAllRoomsForPawn(Pawn p, out bool touchesExterior)
        {
            HashSet<Room> rooms = new HashSet<Room>();
            touchesExterior = false;

            if (p?.Map == null) return rooms;

            foreach (IntVec3 cell in GenAdj.AdjacentCellsAndInside)
            {
                IntVec3 checkPos = p.Position + cell;
                if (checkPos.InBounds(p.Map))
                {
                    Room room = checkPos.GetRoom(p.Map);
                    if (room != null)
                    {
                        if (room.PsychologicallyOutdoors) touchesExterior = true;
                        else rooms.Add(room);
                    }
                }
            }
            return rooms;
        }
		
		//*Furel* Cleans variables to be sure.
        public override void PostOpen()
        {
            base.PostOpen();
            cachedHeights.Clear();
            lastMessageTextSeen = null;
        }
    }
}