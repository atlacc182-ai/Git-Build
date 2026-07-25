@echo off
setlocal

cd /d "%~dp0"
set "ROOT=%~dp0"
set "LOG=%ROOT%Git-Build-build.log"
set "EXE=%ROOT%src\Git-Build.App\bin\Release\net8.0-windows\Git-Build.exe"

echo Closing any running Git-Build window...
taskkill /IM Git-Build.exe /F >nul 2>nul

if not exist "%ROOT%Git-Build.sln" (
    if exist "%EXE%" (
        echo Starting Git-Build...
        start "" "%EXE%"
        exit /b 0
    )
    echo Git-Build.sln and Git-Build.exe were not found in this copied folder.
    pause
    exit /b 1
)

echo Building Git-Build local-folder mode...
dotnet build "%ROOT%Git-Build.sln" -c Release > "%LOG%" 2>&1

if errorlevel 1 (
    echo.
    echo Git-Build did not build. Build log:
    echo %LOG%
    type "%LOG%"
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo Git-Build built, but the EXE was not found:
    echo %EXE%
    pause
    exit /b 1
)

echo Starting Git-Build...
start "" "%EXE%"
