using RimWorld;
using System;
using System.IO;
using System.Text;
using Verse;

namespace EchoColony
{
    public static class EchoDebugLogger
    {
        /// <summary>
        /// Genera o añade un registro en un archivo de texto con el input enviado y el output recibido de la IA.
        /// </summary>
        public static void LogAIInteraction(Pawn pawn, string promptEnviado, string respuestaIA)
        {
            try
            {
                // 📂 Usamos GenFilePaths.SaveDataFolderPath para que guarde de forma segura en:
                // AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\EchoColony_Debug
                string folderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "EchoColony_Debug");

                // Asegurar que la carpeta existe, si no, la crea
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string safePawnName = pawn?.LabelShort ?? "Desconocido";
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safePawnName = safePawnName.Replace(c, '_'); // "Port" mukup se convertirá en _Port_ mukup
                }

                // Creamos un archivo de texto por colono (ej: AI_Log_Echo.txt)
                string fileName = $"AI_Log_{pawn?.LabelShort ?? "Desconocido"}.txt";
                string fullPath = Path.Combine(folderPath, fileName);

                // 📝 Construimos el bloque de texto formateado de forma muy legible
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=================================================================================");
                sb.AppendLine($"TIMESTAMP: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"DÍA EN EL JUEGO: {GenDate.DaysPassed}");
                sb.AppendLine($"COLONO: {pawn?.LabelShort ?? "Desconocido"} (ID: {pawn?.ThingID ?? "N/A"})");
                sb.AppendLine("=================================================================================");
                sb.AppendLine();
                sb.AppendLine("📥 [LO QUE SE ENVIÓ A LA IA (PROMPT + BÚFER)]:");
                sb.AppendLine("---------------------------------------------------------------------------------");
                sb.AppendLine(promptEnviado);
                sb.AppendLine("---------------------------------------------------------------------------------");
                sb.AppendLine();
                sb.AppendLine("📤 [LO QUE LA IA DEVOLVIÓ]:");
                sb.AppendLine("---------------------------------------------------------------------------------");
                sb.AppendLine(string.IsNullOrWhiteSpace(respuestaIA) ? "[ERROR: Respuesta vacía o nula]" : respuestaIA);
                sb.AppendLine("---------------------------------------------------------------------------------");
                sb.AppendLine("\n\n"); // Espaciado para la siguiente interacción del diario

                // 💾 AppendAllText añade el texto al final del archivo sin borrar lo anterior
                File.AppendAllText(fullPath, sb.ToString());

                // Avisamos a la consola del juego para saber que funcionó
                Log.Message($"[EchoColony] Debug log guardado para {pawn?.LabelShort} en: {fullPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Falló la creación del archivo de texto debug de IA: {ex.Message}");
            }
        }
    }
}