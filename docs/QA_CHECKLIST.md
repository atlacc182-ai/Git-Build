# Git-Build QA Checklist

Use this checklist before shipping Git-Build.

## Build Verification

- Restore and build `Git-Build.sln` in Release mode.
- Publish `Git-Build.App` as `Git-Build.exe` for `win-x64`.
- Compile `installer/Git-Build.iss` and confirm the installer is named `Git-Build-Setup.exe`.

## Functional Verification

- Build a public .NET repository.
- Build a public Node.js repository and confirm dependency-step prompting appears.
- Try an SSH remote such as `git@github.com:owner/repo.git`.
- Confirm logs are written to `%LOCALAPPDATA%/Git-Build/Logs`.
- Confirm artifacts can be opened from the Git-Build artifact list.
- Confirm a known failing repository shows a plain-English failure explanation.

## Safety Verification

- Confirm dependency installation is never run without a Git-Build confirmation dialog.
- Confirm every command appears in the live log before it executes.
