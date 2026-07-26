# Git-Build

Git-Build is a Windows desktop Git repository build manager. Paste a public Git repository URL, let Git-Build clone it, detect the build system, confirm dependency steps, build the project, stream live logs, explain build failures in plain English, and open resulting binaries.

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

## Build

```powershell
dotnet restore ./Git-Build.sln
dotnet build ./Git-Build.sln -c Release
```

The executable is produced as `Git-Build.exe` under `src/Git-Build.App/bin/Release/net8.0-windows`.

## Runtime Data

Git-Build stores user data under `%LOCALAPPDATA%/Git-Build`, including `Settings`, `Logs`, and `Repositories`.

## Installer

Install Inno Setup, publish the app, then compile:

```powershell
dotnet publish ./src/Git-Build.App/Git-Build.App.csproj -c Release -r win-x64 --self-contained true
iscc ./installer/Git-Build.iss
```

The intended GitHub repository name is `Git-Build`.
