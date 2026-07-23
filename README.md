# Git-Build

Git-Build is a Windows desktop Git repository build manager. Upload the source code from a local folder, let Git-Build clone it, detect the build system, confirm dependency steps, build the project, stream live logs, explain build failures in plain English, and open resulting binaries.

## Supported Build Systems

- .NET solutions and projects
- Node.js projects with `package.json`
- Python projects with `pyproject.toml` or `requirements.txt`
- Maven and Gradle Java projects
- CMake, Make, Rust/Cargo, Go, Ruby/Bundler, and Docker projects
## Important, READ THIS
-  Don't be an idiot and download anything from the internet.
- Git-Build uses winget to install dependencies so be careful of  what you are installing, it can be manipulated into install a harmul dependencie that changes your computer, irreversibly
- The contributers and creators of Git-Build take NO responsibility if you install a harmfull repository/project,
- The program itself isnot a virus
- Use responsibly

## Project Layout

- `Git-Build.sln` is the solution.
- `src/Git-Build.App` contains the Windows desktop executable, `Git-Build.exe`.
- `src/Git-Build.Core` contains domain models and service contracts.
- `src/Git-Build.Infrastructure` contains Git, process, detection, dependency, logging, artifact, and failure-explanation services.
- `installer/Git-Build.iss` contains the Inno Setup installer script.
- `docs/ARCHITECTURE.md` describes the application architecture.

# Installation

## Prerequisites

Before building Git-Build, ensure you have:

- Windows 10 or Windows 11
- .NET 8 SDK
- Git

---

## Build from Source

### 1. Download the source code

Download the repository as a ZIP file from GitHub or clone it using Git.

### 2. Extract the files

Extract the downloaded ZIP to a folder of your choice.

Example:

```
D:\Git-Build
```

### 3. Open a Command Prompt

Open the extracted project folder in Command Prompt or Windows Terminal.

### 4. Build the project

Run:

```bash
dotnet restore
dotnet build -c Release
```

### 5. Run Git-Build

After the build completes, launch the application using:

```bash
RUN-GIT-BUILD.bat
```

or run the executable directly from:

```
src\Git-Build.App\bin\Release\net8.0-windows\
```

---

## Quick Start

1. Launch Git-Build.
2. Click **Browse**.
3. Select the source code folder of the project you want to build.
4. Click **Build**.
5. Wait for the build to finish.
6. Open the generated executable from the **Artifacts** panel.

---

## Troubleshooting

If the build fails:

- Ensure the .NET 8 SDK is installed.
- Run `dotnet restore` before building.
- Check the live build log for error details.
- Git-Build will explain many common build errors automatically.

```powershell
dotnet publish ./src/Git-Build.App/Git-Build.App.csproj -c Release -r win-x64 --self-contained true
iscc ./installer/Git-Build.iss
```

The intended GitHub repository name is `Git-Build`.
