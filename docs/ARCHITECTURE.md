# Git-Build Architecture

Git-Build is organized as a small layered desktop application.

## Layers

### Git-Build.App

The Windows Forms shell owns user interaction, state transitions, and presentation. It does not contain build-system logic. It wires services together directly to keep the desktop app easy to inspect and package.

### Git-Build.Core

The core project contains immutable models and service interfaces for repository jobs, detection results, dependency requirements, build commands, build artifacts, and failure explanations.

### Git-Build.Infrastructure

Infrastructure contains side-effecting services for Git cloning, process execution, build-system detection, dependency discovery, artifact discovery, failure-log explanation, and Git-Build settings/log/repository folders.

## Build Flow

1. User pastes a public Git remote URL.
2. Git-Build clones the repository into `%LOCALAPPDATA%/Git-Build/Repositories`.
3. Git-Build scans common build files and creates a command plan.
4. Git-Build checks whether required command-line tools are available.
5. Git-Build asks before dependency installation or restore steps.
6. Git-Build runs build commands and streams logs into the UI and log file.
7. On success, Git-Build lists binary/package artifacts.
8. On failure, Git-Build summarizes likely causes and suggested fixes in plain English.

## Extension Points

Add a build system by updating `BuildSystemDetector` and, if needed, adding a specialized service behind the existing interfaces. Build commands are represented by `CommandSpec`, so most build systems do not need UI changes.

## Security Posture

Git-Build treats public repositories as untrusted code. It asks before dependency commands and displays every command it runs. Builds still execute local code, so users should only build repositories they trust.
