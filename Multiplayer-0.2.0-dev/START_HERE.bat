@echo off
chcp 65001 >nul
title MSC Multiplayer 0.2.0-dev
echo === MSC Multiplayer 0.2.0-dev ===
where dotnet >nul 2>nul || (echo [ERROR] net SDK ne nayden. Ustanovi .NET SDK 8+ & pause & exit /b 1)
if not defined MSC_GAME_DIR set /p MSC_GAME_DIR="Put k papke My Summer Car (gde mysummercar.exe): "
if not exist "%MSC_GAME_DIR%\mysummercar_Data\Managed\MSCLoader.dll" (
  echo [ERROR] MSCLoader.dll ne nayden v "%MSC_GAME_DIR%"
  pause & exit /b 1
)
echo [1/3] Sborka servera...
dotnet build "%~dp0src\MSC.Server\MSC.Server.csproj" -c Release || (pause & exit /b 1)
echo [2/3] Sborka moda...
dotnet build "%~dp0src\MSC.Client\MSC.Client.csproj" -c Release || (pause & exit /b 1)
echo [3/3] Gotovo.
echo   Mod:    %~dp0src\MSC.Client\bin\Release\net35\MSC.Multiplayer.Client.dll
echo   Server: %~dp0src\MSC.Server\bin\Release\net8.0\MSC.Server.exe
echo Skopiruy DLL v papku Mods MSCLoader, zapusti server, v igre nazhmi F8.
set /p RUN="Zapustit server seychas? (y/n): "
if /i "%RUN%"=="y" "%~dp0src\MSC.Server\bin\Release\net8.0\MSC.Server.exe"
pause