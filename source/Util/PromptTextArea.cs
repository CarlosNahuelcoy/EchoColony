using UnityEngine;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Multiline text areas for prompts. Stops Enter / Shift+Enter from reaching the
    /// host window's "accept" handler (which closes the window or the mod settings dialog),
    /// so the key inserts a new line instead.
    /// </summary>
    public static class PromptTextArea
    {
        public static string Draw(Rect rect, string text, string controlName)
        {
            GUI.SetNextControlName(controlName);
            string result = Widgets.TextArea(rect, text ?? "");
            SwallowEnterIfFocused(controlName);
            return result;
        }

        public static void SwallowEnterIfFocused(string controlName)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            if (GUI.GetNameOfFocusedControl() != controlName) return;

            // The text area inserts the newline from the character event; this only
            // consumes the key event so the window doesn't treat it as "accept".
            e.Use();
        }
    }
}
