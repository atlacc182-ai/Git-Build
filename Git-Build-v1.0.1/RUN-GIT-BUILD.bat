@echo off
setlocal
echo Closing any running Git-Build window...
taskkill /IM Git-Build.exe /F >nul 2>nul

echo Building Git-Build local-folder mode...
dotnet build "C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\Git-Build.sln" -c Release > "C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\Git-Build-build.log" 2>&1
if errorlevel 1 goto failed

echo Starting Git-Build...
start "" "C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\src\Git-Build.App\bin\Release\net8.0-windows\Git-Build.exe"
goto done

:failed
echo.
echo Git-Build did not build. Build log:
echo C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\Git-Build-build.log
type "C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\Git-Build-build.log"
pause
exit /b 1

:done
echo Git-Build started. If it did not open, check:
echo C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\src\Git-Build.App\bin\Release\net8.0-windows\Git-Build.exe
endlocal
