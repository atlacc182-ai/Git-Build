@echo off
setlocal
set EXE=C:\Users\Reansh Tiwari\Documents\Codex\2026-07-02\you-are-a-senior-software-engineer\src\Git-Build.App\bin\Release\net8.0-windows\Git-Build.exe
if not exist "%EXE%" (
  echo Git-Build.exe was not found.
  echo Run RUN-GIT-BUILD.bat first.
  pause
  exit /b 1
)
echo Opening Git-Build from:
echo %EXE%
start "" "%EXE%"
endlocal
