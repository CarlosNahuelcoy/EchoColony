echo off

REM remove previous builds (1.6 and 1.7)
DEL .\1.6\Assemblies\*.* /Q 2>NUL
DEL .\1.7\Assemblies\*.* /Q 2>NUL

REM RimWorld 1.7 (net10.0) needs the .NET 10 SDK; use the per-user install if present
set DOTNET=dotnet
if exist "%LOCALAPPDATA%\Microsoft\dotnet10\dotnet.exe" set DOTNET="%LOCALAPPDATA%\Microsoft\dotnet10\dotnet.exe"

REM build both DLLs from the single project: 1.6 (net472) and 1.7 (net10.0)
%DOTNET% build source\mod.csproj
