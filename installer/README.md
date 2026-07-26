# Git-Build Installer

Run this from the repository root:

```bat
BUILD-INSTALLER.bat
```

It creates a single downloadable installer:

```text
Git-Build-Setup.exe
```

That one `.exe` contains:

- the Git-Build app
- the .NET runtime needed to run it
- all app DLLs/assets
- shortcut creation logic

Users on another Windows PC only need to download and run `Git-Build-Setup.exe`.

The installer tries to install to:

```text
C:\Program Files\Git-Build
```

If it does not have permission, it installs to:

```text
%LOCALAPPDATA%\Git-Build\App
```

It can create Desktop and Start Menu shortcuts.

Windows integration:

- creates a Start Menu shortcut so Git-Build appears in Windows Search
- registers `Git-Build.exe` under the current user's App Paths
- registers Git-Build in Windows Apps/Installed Apps with an uninstall command

Uninstall options:

- run `Git-Build-Setup.exe` and click `Uninstall`
- or run the installed uninstaller:

```bat
"%LOCALAPPDATA%\Git-Build\App\Git-Build-Uninstall.exe" /uninstall
```

If Git-Build was installed to Program Files, the uninstaller is stored there instead.
