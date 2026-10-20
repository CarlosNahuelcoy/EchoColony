# Contribuir a EchoColony

EchoColony se publica para **RimWorld 1.6 y 1.7 desde el mismo código**. No hay ramas ni carpetas de código por versión: un solo proyecto compila las dos DLL.

## Compilar

| Versión | Target | Salida | Harmony |
|---|---|---|---|
| RimWorld 1.6 | `net472` | `1.6/Assemblies/EchoColony.dll` | 2.x |
| RimWorld 1.7 | `net10.0` | `1.7/Assemblies/EchoColony.dll` | 3.x |

- Hace falta el **SDK de .NET 10** (compila los dos targets). En Windows, `.vscode/build.bat` usa `%LOCALAPPDATA%\Microsoft\dotnet10\dotnet.exe` si existe.
- Desde la raíz del repo: `dotnet build source/mod.csproj` o la tarea "build dll" de VS Code.
- **No se publica `0Harmony.dll`**: el mod depende de Harmony (`brrainz.harmony`). Se usa `Lib.Harmony.Ref`, que solo sirve para compilar.
- No se usa un Assembly-CSharp "publicizado". Si necesitas un campo o método privado del juego, usa la inyección de Harmony (`___campo`), `AccessTools` o `Traverse`. Si algo de la API pública del juego hace lo mismo, mejor.

GitHub Actions compila **las dos versiones** en cada PR y en cada push a `main` (`.github/workflows/build.yml`). Si el check falla, el cambio rompió una de las dos: revísalo antes de fusionar.

## Código distinto por versión

Casi todo el código es igual en 1.6 y 1.7. Cuando no lo sea:

- **Diferencias pequeñas:** un bloque `#if RW17` (el símbolo `RW17` solo existe al compilar para 1.7):

  ```csharp
  #if RW17
      // código para RimWorld 1.7
  #else
      // código para RimWorld 1.6
  #endif
  ```

- **Diferencias grandes:** un archivo aparte en `source/Compat/RW16/` o `source/Compat/RW17/`. El proyecto incluye cada carpeta solo en su versión. Mantén la misma clase o los mismos métodos en ambos archivos para que el resto del código no note la diferencia.

Nunca copies un archivo entero para "la otra versión" fuera de `source/Compat/`: dos copias divergen enseguida.

## Harmony 3 (RimWorld 1.7) es más estricto

Rechaza parches cuyos tipos no coinciden exactamente con el método original:

- `__instance` debe ser del tipo que declara el método (o una base suya).
- `__result` debe tener exactamente el tipo de retorno (por ejemplo, `IEnumerable<Gizmo>`, no `object`).
- `___campo` debe tener exactamente el tipo del campo privado.
- Los demás parámetros deben llamarse igual que en el original y tener el mismo tipo.
- `__state` debe tener el mismo tipo en el prefix y en el postfix.

Los parches se aplican **clase por clase** (`Main.cs`): si uno falla, sale un aviso en el log y los demás siguen funcionando.

## Carpetas del mod

```
About/          metadatos (supportedVersions incluye 1.5, 1.6 y 1.7)
Common/         Defs, Languages, Patches y Textures compartidos por 1.6 y 1.7
Mods/           contenido que solo se carga si un DLC o mod está activo
Textures/       texturas compartidas (retratos de storytellers, etc.)
1.6/Assemblies  DLL compilada para 1.6
1.7/Assemblies  DLL compilada para 1.7
LoadFolders.xml qué carpetas carga cada versión
```

- Defs, traducciones, parches XML y texturas nuevos van en **`Common/`**, nunca en `1.6/` o `1.7/`: si estuvieran en ambos sitios se cargarían dos veces.
- **1.5 está congelada:** su carpeta `1.5/` solo existe en la copia publicada en Steam y no se actualiza.
- Al publicar, la carpeta del mod debe tener `About`, `Common`, `Mods`, `Textures`, `LoadFolders.xml`, `1.5`, y `1.6/Assemblies` y `1.7/Assemblies` recién compiladas.
