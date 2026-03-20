using System;
using System.Collections.Generic;
using Verse;

namespace EchoColony.Conversations
{
    // ── Data ─────────────────────────────────────────────────────────────────────

    public class PendingBubble
    {
        public Pawn   pawn;
        public string text;
        public int    scheduledTick;
    }

    // ── Sequencer ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Queues a set of speech bubbles and fires them at timed intervals using
    /// RimWorld game ticks. Thread-safe to enqueue from a coroutine.
    /// </summary>
    public class BubbleSequencer : IExposable
    {
        private Queue<PendingBubble> queue = new Queue<PendingBubble>();
        private bool active = false;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueue a set of (pawn, line) pairs with staggered timing.
        /// </summary>
        /// <param name="lines">Ordered list of (speaker, text) pairs.</param>
        /// <param name="delaySeconds">Seconds between each bubble.</param>
        public void Enqueue(List<(Pawn speaker, string text)> lines, float delaySeconds = 1.5f)
        {
            if (lines == null || lines.Count == 0) return;

            int delayTicks = Math.Max(1, (int)(delaySeconds * 60f));

            // Start AFTER the last already-queued bubble so conversations don't overlap.
            // If the queue is empty, start from now.
            int startTick = Find.TickManager.TicksGame;
            if (queue.Count > 0)
            {
                // Walk the queue to find the furthest scheduled tick.
                // Queue<T> supports foreach without dequeuing. Cost is negligible (queue is tiny).
                foreach (var pending in queue)
                    if (pending.scheduledTick > startTick)
                        startTick = pending.scheduledTick;
                startTick += delayTicks; // one extra gap after the last bubble
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var (speaker, text) = lines[i];
                if (speaker == null || string.IsNullOrWhiteSpace(text)) continue;

                queue.Enqueue(new PendingBubble
                {
                    pawn          = speaker,
                    text          = text,
                    scheduledTick = startTick + (i * delayTicks)
                });
            }

            if (queue.Count > 0) active = true;
        }

        // ── Tick ──────────────────────────────────────────────────────────────────

        public void Tick()
        {
            if (!active || queue.Count == 0) return;

            int now = Find.TickManager.TicksGame;

            while (queue.Count > 0 && now >= queue.Peek().scheduledTick)
            {
                var bubble = queue.Dequeue();
                if (bubble.pawn != null && !bubble.pawn.Dead && bubble.pawn.Spawned)
                    BubbleController.ShowBubble(bubble.pawn, bubble.text);
            }

            if (queue.Count == 0) active = false;
        }

        public bool IsActive   => active;
        public int  PendingCount => queue.Count;

        // Bubbles are ephemeral — nothing to save.
        public void ExposeData() { }
    }

    // ── MapComponent wrapper ──────────────────────────────────────────────────────

    public class BubbleSequencerComponent : MapComponent
    {
        private BubbleSequencer sequencer;

        public BubbleSequencer Sequencer
        {
            get
            {
                if (sequencer == null) sequencer = new BubbleSequencer();
                return sequencer;
            }
        }

        public BubbleSequencerComponent(Map map) : base(map)
        {
            sequencer = new BubbleSequencer();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            try { sequencer?.Tick(); }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] BubbleSequencer tick error: {ex.Message}");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Sequencer state is not persisted (ephemeral visual effect).
        }
    }
}