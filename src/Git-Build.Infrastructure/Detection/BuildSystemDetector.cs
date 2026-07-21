using System.Text.RegularExpressions;
using GitBuild.Core.Models;
using GitBuild.Core.Services;

namespace GitBuild.Infrastructure.Detection;

public sealed class BuildSystemDetector : IBuildSystemDetector
{
    public Task<DetectionResult> DetectAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Take(4000)
            .ToArray();

        DetectionResult result;
        var solution = Find(files, ".sln", repositoryPath);
        if (solution is not null && IsVisualStudioCppSolution(solution, files))
        {
            result = VisualStudioCpp(repositoryPath, solution);
        }
        else if (solution is not null)
        {
            result = DotNet(repositoryPath, solution, $"Found {Path.GetRelativePath(repositoryPath, solution)}.");
        }
        else if (Find(files, ".csproj", repositoryPath) is { } csproj)
        {
            result = DotNet(repositoryPath, csproj, $"Found {Path.GetRelativePath(repositoryPath, csproj)}.");
        }
        else if (FindByName(files, "package.json", repositoryPath) is { } packageJson) result = Node(repositoryPath, packageJson);
        else if (FindAtRoot(files, "pyproject.toml", repositoryPath) is { } rootPyproject) result = Python(repositoryPath, rootPyproject);
        else if (FindAtRoot(files, "requirements.txt", repositoryPath) is { } rootRequirements) result = Python(repositoryPath, rootRequirements);
        else if (FindByName(files, "pom.xml", repositoryPath) is { } pom) result = Maven(repositoryPath, pom);
        else if (FindByName(files, "build.gradle", repositoryPath) is { } gradle) result = Gradle(repositoryPath, gradle);
        else if (FindByName(files, "build.gradle.kts", repositoryPath) is { } gradleKts) result = Gradle(repositoryPath, gradleKts);
        else if (FindByName(files, "CMakeLists.txt", repositoryPath) is { } cmake) result = CMake(repositoryPath, cmake);
        else if (FindByName(files, "Cargo.toml", repositoryPath) is { } cargo) result = Cargo(repositoryPath, cargo);
        else if (FindByName(files, "go.mod", repositoryPath) is { } goMod) result = Go(repositoryPath, goMod);
        else if (FindByName(files, "Gemfile", repositoryPath) is { } gemfile) result = Ruby(repositoryPath, gemfile);
        else if (FindByName(files, "Dockerfile", repositoryPath) is { } dockerfile) result = Docker(repositoryPath, dockerfile);
        else if (FindByName(files, "Makefile", repositoryPath) is { } makefile) result = Make(repositoryPath, makefile);
        else if (FindByName(files, "pyproject.toml", repositoryPath) is { } pyproject) result = Python(repositoryPath, pyproject);
        else if (FindByName(files, "requirements.txt", repositoryPath) is { } requirements) result = Python(repositoryPath, requirements);
        else result = Unknown();

        return Task.FromResult(result);
    }

    private static bool IsVisualStudioCppSolution(string solutionPath, IEnumerable<string> files)
    {
        if (files.Any(path => path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        try
        {
            return File.ReadAllText(solutionPath).Contains(".vcxproj", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
    private static bool Has(IEnumerable<string> files, string fileName) =>
        files.Any(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));

    private static string? Find(IEnumerable<string> files, string extension, string root) =>
        files.Where(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(root, path).Count(ch => ch == Path.DirectorySeparatorChar))
            .FirstOrDefault();

    private static string? FindByName(IEnumerable<string> files, string fileName, string root) =>
        files.Where(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(root, path).Count(ch => ch == Path.DirectorySeparatorChar))
            .FirstOrDefault();

    private static string? FindAtRoot(IEnumerable<string> files, string fileName, string root) =>
        files.FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase));

    private static string FolderOf(string path, string fallbackRoot) =>
        Path.GetDirectoryName(path) ?? fallbackRoot;

    private static DependencyRequirement Need(string name, string exe, string hint) =>
        new(name, exe, hint, WingetInstall(name, exe));

    private static DependencyRequirement NeedMsvcBuildTools() =>
        new(
            "Visual Studio C++ Build Tools",
            "link.exe",
            "Install Visual Studio Build Tools with the Desktop development with C++ workload.",
            new CommandSpec(
                "powershell",
                "-NoProfile -ExecutionPolicy Bypass -Command \"winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --accept-package-agreements --accept-source-agreements --override '--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended'\"",
                Environment.CurrentDirectory,
                "Install Visual Studio C++ Build Tools with winget",
                true));

    private static CommandSpec? WingetInstall(string name, string exe)
    {
        if (exe.Equals("gradle", StringComparison.OrdinalIgnoreCase))
        {
            var command = "-NoProfile -ExecutionPolicy Bypass -Command \"$tools = Join-Path $env:LOCALAPPDATA 'Git-Build\\Tools'; " +
                "New-Item -ItemType Directory -Force -Path $tools | Out-Null; " +
                "$current = Invoke-RestMethod 'https://services.gradle.org/versions/current'; " +
                "$version = $current.version; " +
                "$downloadUrl = $current.downloadUrl; " +
                "$zip = Join-Path $tools ('gradle-' + $version + '-bin.zip'); " +
                "$installDir = Join-Path $tools ('gradle-' + $version); " +
                "if (!(Test-Path (Join-Path $installDir 'bin\\gradle.bat'))) { " +
                "Invoke-WebRequest -Uri $downloadUrl -OutFile $zip; " +
                "Expand-Archive -LiteralPath $zip -DestinationPath $tools -Force; " +
                "Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue }; " +
                "Write-Output ('Gradle installed at ' + $installDir)\"";

            return new CommandSpec("powershell", command, Environment.CurrentDirectory, "Install Gradle from official distribution", true);
        }

        var packageId = exe.ToLowerInvariant() switch
        {
            "dotnet" => "Microsoft.DotNet.SDK.8",
            "node" => "OpenJS.NodeJS.LTS",
            "python" => "Python.Python.3.12",
            "java" => "EclipseAdoptium.Temurin.21.JDK",
            "mvn" => "Apache.Maven",
            "cmake" => "Kitware.CMake",
            "cargo" => "Rustlang.Rustup",
            "go" => "GoLang.Go",
            "ruby" => "RubyInstallerTeam.RubyWithDevKit.3.3",
            "bundle" => "RubyInstallerTeam.RubyWithDevKit.3.3",
            "docker" => "Docker.DockerDesktop",
            _ => null
        };

        return packageId is null
            ? null
            : new CommandSpec("winget", $"install --id {packageId} --exact --accept-package-agreements --accept-source-agreements", Environment.CurrentDirectory, $"Install {name} with winget", true);
    }


    private static DetectionResult VisualStudioCpp(string root, string solutionPath)
    {
        var solutionName = Path.GetFileName(solutionPath);
        var solutionFolder = FolderOf(solutionPath, root);
        var relativeSolution = Path.GetRelativePath(root, solutionPath);
        var command = "-NoProfile -ExecutionPolicy Bypass -Command \"$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\\Installer\\vswhere.exe'; if (!(Test-Path $vswhere)) { Write-Error 'Install Visual Studio Build Tools 2022 with Desktop development with C++.'; exit 1 }; $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\\**\\Bin\\MSBuild.exe' | Select-Object -First 1; if (!$msbuild) { Write-Error 'MSBuild was not found. Install Visual Studio Build Tools 2022 with Desktop development with C++.'; exit 1 }; & $msbuild '$solutionName' /m /p:Configuration=Release /p:Platform=x64; exit $LASTEXITCODE\"";
        return new DetectionResult(
            BuildSystemKind.VisualStudioCpp,
            "Visual Studio C++",
            $"Found C++ solution {relativeSolution}.",
            [Need("Visual Studio Build Tools", "msbuild", "Install Visual Studio Build Tools 2022 with Desktop development with C++.")],
            [
                new("powershell", command, solutionFolder, "Build Visual Studio C++ solution"),
                new("powershell", CollectArtifactsCommand(".", "*.exe", "*.dll", "*.lib", "*.msi", "*.zip"), solutionFolder, "Collect Visual Studio C++ artifacts")
            ]);
    }
    private static DetectionResult DotNet(string root, string projectFile, string reason)
    {
        var workingDirectory = FolderOf(projectFile, root);
        var projectName = Path.GetFileName(projectFile);

        return new DetectionResult(
            BuildSystemKind.DotNet,
            ".NET",
            reason,
            [NeedDotNetSdk(projectFile, workingDirectory)],
            [
                new("dotnet", $"restore \"{projectName}\"", workingDirectory, ".NET restore"),
                new("dotnet", $"build \"{projectName}\" --configuration Release --no-restore", workingDirectory, ".NET build"),
                new("powershell", CollectArtifactsCommand("bin", "*.exe", "*.dll", "*.nupkg", "*.msi", "*.zip"), workingDirectory, "Collect .NET artifacts")
            ]);
    }

    private static DependencyRequirement NeedDotNetSdk(string projectFile, string workingDirectory)
    {
        var major = DetectHighestDotNetTargetMajor(projectFile, workingDirectory);
        if (major >= 9)
        {
            return new DependencyRequirement(
                ".NET 9 SDK",
                "dotnet-sdk-9",
                "Install the .NET 9 SDK from https://dotnet.microsoft.com/download",
                new CommandSpec(
                    "winget",
                    "install --id Microsoft.DotNet.SDK.9 --exact --accept-package-agreements --accept-source-agreements",
                    Environment.CurrentDirectory,
                    "Install .NET 9 SDK with winget",
                    true));
        }

        return Need(".NET SDK", "dotnet", "Install the .NET SDK from https://dotnet.microsoft.com/download");
    }

    private static int DetectHighestDotNetTargetMajor(string projectFile, string workingDirectory)
    {
        var projectFiles = projectFile.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            ? Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)
            : [projectFile];

        return projectFiles
            .SelectMany(ReadTargetFrameworkMajors)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static IEnumerable<int> ReadTargetFrameworkMajors(string projectFile)
    {
        string text;
        try
        {
            text = File.ReadAllText(projectFile);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(text, @"<TargetFrameworks?>(?<value>[^<]+)</TargetFrameworks?>", RegexOptions.IgnoreCase))
        {
            foreach (var framework in match.Groups["value"].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var version = Regex.Match(framework, @"net(?<major>\d+)(?:\.\d+)?", RegexOptions.IgnoreCase);
                if (version.Success && int.TryParse(version.Groups["major"].Value, out var major))
                {
                    yield return major;
                }
            }
        }
    }

    private static DetectionResult Node(string root, string packageJson)
    {
        var workingDirectory = FolderOf(packageJson, root);
        var packageText = File.Exists(packageJson) ? File.ReadAllText(packageJson) : string.Empty;
        var packageManager = DetectPackageManager(workingDirectory, packageText);
        var hasNodeModules = Directory.Exists(Path.Combine(workingDirectory, "node_modules"));
        var needsTurbo = packageText.Contains("turbo", StringComparison.OrdinalIgnoreCase);
        var turboShimPath = OperatingSystem.IsWindows()
            ? Path.Combine(workingDirectory, "node_modules", ".bin", "turbo.cmd")
            : Path.Combine(workingDirectory, "node_modules", ".bin", "turbo");
        var turboModulePath = Path.Combine(workingDirectory, "node_modules", "turbo", "bin", "turbo");
        var hasRequiredBuildTools = !needsTurbo || (File.Exists(turboShimPath) && File.Exists(turboModulePath));
        var canSkipInstall = hasNodeModules && hasRequiredBuildTools;
        var relativePackageJson = Path.GetRelativePath(root, packageJson);

        if (!canSkipInstall)
        {
            RepairNodeModules(workingDirectory);
        }

        var commands = CreateNodeCommands(workingDirectory, packageManager, canSkipInstall);
        var reason = canSkipInstall
            ? $"Found {relativePackageJson} and complete node_modules. Using {packageManager} and skipping install for a faster repeat build."
            : hasNodeModules
                ? $"Found {relativePackageJson} for {packageManager}, but node_modules looks incomplete or corrupted. Repairing packages."
                : $"Found {relativePackageJson} for {packageManager}.";

        return new DetectionResult(
            BuildSystemKind.Node,
            $"Node.js ({packageManager})",
            reason,
            [Need("Node.js", "node", "Install Node.js LTS from https://nodejs.org"), Need(packageManager, packageManager == "npm" ? "npm" : "corepack", "Install Node.js with Corepack enabled, or install the package manager named in package.json.")],
            commands);
    }

    private static string DetectPackageManager(string root, string packageText)
    {
        if (packageText.Contains("\"packageManager\": \"yarn@", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(root, "yarn.lock")))
        {
            return "yarn";
        }

        if (packageText.Contains("\"packageManager\": \"pnpm@", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(root, "pnpm-lock.yaml")))
        {
            return "pnpm";
        }

        if (packageText.Contains("\"packageManager\": \"bun@", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(root, "bun.lockb")))
        {
            return "bun";
        }

        return "npm";
    }

    private static CommandSpec[] CreateNodeCommands(string root, string packageManager, bool canSkipInstall)
    {
        var install = packageManager switch
        {
            "yarn" => new CommandSpec("cmd.exe", "/d /c corepack yarn install", root, "Install Yarn packages", true),
            "pnpm" => new CommandSpec("cmd.exe", "/d /c corepack pnpm install", root, "Install pnpm packages", true),
            "bun" => new CommandSpec("bun", "install", root, "Install Bun packages", true),
            _ => new CommandSpec("npm", "install --legacy-peer-deps --no-audit --no-fund --prefer-offline", root, "Install npm packages", true)
        };

        var build = packageManager switch
        {
            "yarn" => new CommandSpec("cmd.exe", "/d /c corepack yarn run build", root, "Run Yarn build"),
            "pnpm" => new CommandSpec("cmd.exe", "/d /c corepack pnpm run build", root, "Run pnpm build"),
            "bun" => new CommandSpec("bun", "run build", root, "Run Bun build"),
            _ => new CommandSpec("npm", "run build --if-present", root, "Run npm build")
        };

        var collect = new CommandSpec("powershell", NodeArtifactCommand(), root, "Collect Node artifacts");

        return canSkipInstall ? [build, collect] : [install, build, collect];
    }

    private static void RepairNodeModules(string root)
    {
        var paths = new[]
        {
            Path.Combine(root, "node_modules", ".bin", OperatingSystem.IsWindows() ? "turbo.cmd" : "turbo"),
            Path.Combine(root, "node_modules", "turbo"),
            Path.Combine(root, "node_modules", "esbuild"),
            Path.Combine(root, "node_modules", "@esbuild"),
            Path.Combine(root, "node_modules", "vite", "node_modules", "esbuild"),
            Path.Combine(root, "node_modules", "vite", "node_modules", "@esbuild")
        };

        foreach (var item in paths)
        {
            TryDelete(item);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // npm/yarn can still repair most installs even if Windows locks a stale folder.
        }
    }

    private static DetectionResult Python(string root, string projectFile)
    {
        var fileName = Path.GetFileName(projectFile);
        var workingDirectory = Path.GetDirectoryName(projectFile) ?? root;
        var relativeFile = Path.GetRelativePath(root, projectFile);
        var pipOptions = "--disable-pip-version-check --no-warn-script-location";
        var buildToolsCommand = $"-m pip install {pipOptions} --upgrade setuptools wheel";
        var usesCustomSetupBuild = fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(Path.Combine(workingDirectory, "setup.py")) &&
            !File.ReadAllText(projectFile).Contains("build-backend", StringComparison.OrdinalIgnoreCase);
        var installCommand = fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase)
            ? $"-m pip install {pipOptions} -r requirements.txt"
            : usesCustomSetupBuild
                ? SetupPyBuildCommand()
                : $"-m pip install {pipOptions} --no-build-isolation .";
        var installDisplayName = usesCustomSetupBuild ? "Build Python project with setup.py" : "Install Python packages";
        var requirements = usesCustomSetupBuild
            ? new[] { Need("Python", "python", "Install Python from https://www.python.org/downloads/"), NeedMsvcBuildTools() }
            : [Need("Python", "python", "Install Python from https://www.python.org/downloads/")];

        return new DetectionResult(
            BuildSystemKind.Python,
            "Python",
            $"Found {relativeFile}.",
            requirements,
            [
                new("python", $"-m pip install {pipOptions} --upgrade pip", workingDirectory, "Upgrade pip automatically", true),
                new("python", buildToolsCommand, workingDirectory, "Install Python build tools", true),
                usesCustomSetupBuild
                    ? new("powershell", installCommand, workingDirectory, installDisplayName, true)
                    : new("python", installCommand, workingDirectory, installDisplayName, true),
                new("python", "-m compileall .", workingDirectory, "Compile Python sources"),
                new("powershell", CollectArtifactsCommand("build", "*.exe", "*.dll", "*.pyd", "*.so", "*.zip", "*.tar", "*.gz"), workingDirectory, "Collect Python build artifacts"),
                new("powershell", PythonLauncherCommand(), workingDirectory, "Create Python launchers")
            ]);
    }

    private static string SetupPyBuildCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\\Installer\\vswhere.exe'; " +
        "$vsdev = $null; " +
        "if (Test-Path $vswhere) { $vsdev = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'Common7\\Tools\\VsDevCmd.bat' | Select-Object -First 1 }; " +
        "if ($vsdev) { $bat = Join-Path $env:TEMP ('git-build-python-' + [Guid]::NewGuid().ToString('N') + '.cmd'); " +
        "$lines = @('@echo off', 'call \"' + $vsdev + '\" -arch=x64 -host_arch=x64', 'python setup.py build'); " +
        "Set-Content -LiteralPath $bat -Value $lines -Encoding ASCII; & $bat; $exitCode = $LASTEXITCODE; Remove-Item -LiteralPath $bat -Force -ErrorAction SilentlyContinue; exit $exitCode } else { python setup.py build }; " +
        "exit $LASTEXITCODE\"";

    private static string PythonLauncherCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
        "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
        "$scripts = Get-ChildItem -Path . -Filter *.py -Recurse | Where-Object { $_.FullName -notmatch '\\\\(__pycache__|\\.venv|venv|env|\\.git|Git-Build-Artifacts)\\\\' -and $_.Name -ne '__init__.py' }; " +
        "$runnable = @($scripts | Where-Object { Select-String -LiteralPath $_.FullName -Pattern '__main__' -Quiet }); " +
        "if ($runnable.Count -eq 0) { $rootPath = (Get-Location).Path; $runnable = @($scripts | Where-Object { $_.DirectoryName -eq $rootPath } | Select-Object -First 5) }; " +
        "foreach ($script in ($runnable | Select-Object -First 10)) { $name = [IO.Path]::GetFileNameWithoutExtension($script.Name); $cmd = Join-Path $artifactDir ($name + '.cmd'); $content = '@echo off' + [Environment]::NewLine + 'cd /d \"' + $script.DirectoryName + '\"' + [Environment]::NewLine + 'python \"' + $script.FullName + '\" %*' + [Environment]::NewLine; Set-Content -LiteralPath $cmd -Value $content -Encoding ASCII }; " +
        "if ($runnable.Count -eq 0) { $cmd = Join-Path $artifactDir 'open-python-folder.cmd'; $content = '@echo off' + [Environment]::NewLine + 'cd /d \"' + (Get-Location).Path + '\"' + [Environment]::NewLine + 'explorer .'+ [Environment]::NewLine; Set-Content -LiteralPath $cmd -Value $content -Encoding ASCII; Set-Content -LiteralPath (Join-Path $artifactDir 'README.txt') -Value 'Python build succeeded. This project did not expose a runnable __main__ script, so Git-Build created a folder launcher.' -Encoding UTF8 }; " +
        "Write-Output ('Created Python launchers in ' + $artifactDir)\"";

    private static DetectionResult Maven(string root, string pomFile)
    {
        var workingDirectory = FolderOf(pomFile, root);
        return new DetectionResult(
            BuildSystemKind.Maven,
            "Maven",
            $"Found {Path.GetRelativePath(root, pomFile)}.",
            [Need("Java", "java", "Install a JDK."), Need("Maven", "mvn", "Install Apache Maven.")],
            [
                new("mvn", "dependency:go-offline", workingDirectory, "Download Maven dependencies", true),
                new("mvn", "package -DskipTests", workingDirectory, "Maven package"),
                new("powershell", CollectArtifactsCommand("target", "*.jar", "*.war", "*.ear", "*.zip"), workingDirectory, "Collect Maven artifacts")
            ]);
    }

    private static DetectionResult Gradle(string root, string gradleFile)
    {
        var workingDirectory = FolderOf(gradleFile, root);
        var hasWrapper = File.Exists(Path.Combine(workingDirectory, "gradlew.bat"));
        var isAndroidProject = IsAndroidGradleProject(workingDirectory);
        var requirements = hasWrapper
            ? new List<DependencyRequirement> { Need("Java", "java", "Install a JDK.") }
            : [Need("Java", "java", "Install a JDK."), Need("Gradle", "gradle", "Install Gradle.")];
        if (isAndroidProject)
        {
            requirements.Add(NeedAndroidSdk());
        }

        var commands = new List<CommandSpec>();
        if (isAndroidProject)
        {
            commands.Add(new CommandSpec("powershell", EnsureAndroidLocalPropertiesCommand(), workingDirectory, "Prepare Android SDK local.properties"));
        }

        if (hasWrapper)
        {
            commands.Add(new CommandSpec(Path.Combine(workingDirectory, "gradlew.bat"), "--refresh-dependencies build", workingDirectory, "Gradle wrapper build", true));
        }
        else
        {
            commands.Add(new CommandSpec("gradle", "--refresh-dependencies build", workingDirectory, "Gradle build", true));
        }

        commands.Add(new CommandSpec("powershell", CollectArtifactsCommand("build", "*.jar", "*.war", "*.zip", "*.apk", "*.aab"), workingDirectory, "Collect Gradle artifacts"));

        return new DetectionResult(
            BuildSystemKind.Gradle,
            "Gradle",
            $"Found {Path.GetRelativePath(root, gradleFile)}.",
            requirements,
            commands);
    }

    private static bool IsAndroidGradleProject(string workingDirectory)
    {
        return File.Exists(Path.Combine(workingDirectory, "settings.gradle")) &&
            (Directory.Exists(Path.Combine(workingDirectory, "app")) ||
             Directory.EnumerateFiles(workingDirectory, "AndroidManifest.xml", SearchOption.AllDirectories).Any() ||
             Directory.EnumerateFiles(workingDirectory, "build.gradle", SearchOption.AllDirectories)
                 .Any(path => File.ReadAllText(path).Contains("com.android.", StringComparison.OrdinalIgnoreCase)));
    }

    private static DependencyRequirement NeedAndroidSdk() =>
        new(
            "Android SDK",
            "android-sdk",
            "Install Android Studio or Android command-line tools.",
            new CommandSpec("winget", "install --id Google.AndroidStudio --exact --accept-package-agreements --accept-source-agreements", Environment.CurrentDirectory, "Install Android Studio with winget", true));

    private static string EnsureAndroidLocalPropertiesCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$sdk = $env:ANDROID_HOME; " +
        "if ([string]::IsNullOrWhiteSpace($sdk)) { $sdk = $env:ANDROID_SDK_ROOT }; " +
        "if ([string]::IsNullOrWhiteSpace($sdk)) { $candidate = Join-Path $env:LOCALAPPDATA 'Android\\Sdk'; if (Test-Path $candidate) { $sdk = $candidate } }; " +
        "if ([string]::IsNullOrWhiteSpace($sdk) -or !(Test-Path $sdk)) { Write-Error 'Android SDK was not found. Install Android Studio once, open it, and let it install the Android SDK.'; exit 1 }; " +
        "$escaped = $sdk.Replace('\\\\', '\\\\\\\\'); " +
        "Set-Content -LiteralPath 'local.properties' -Value ('sdk.dir=' + $escaped) -Encoding ASCII; " +
        "Write-Output ('Created local.properties with Android SDK at ' + $sdk)\"";

    private static DetectionResult CMake(string root, string cmakeFile)
    {
        var workingDirectory = FolderOf(cmakeFile, root);
        return new DetectionResult(
            BuildSystemKind.CMake,
            "CMake",
            $"Found {Path.GetRelativePath(root, cmakeFile)}.",
            [Need("CMake", "cmake", "Install CMake."), Need("Compiler", "cl", "Install Visual Studio Build Tools or another C/C++ toolchain.")],
            [
                new("cmake", "-S . -B build", workingDirectory, "Configure CMake"),
                new("cmake", "--build build --config Release", workingDirectory, "Build CMake project"),
                new("powershell", CollectArtifactsCommand("build", "*.exe", "*.dll", "*.lib", "*.zip", "*.msi"), workingDirectory, "Collect CMake artifacts")
            ]);
    }

    private static DetectionResult Cargo(string root, string cargoFile)
    {
        var workingDirectory = FolderOf(cargoFile, root);
        return new DetectionResult(
            BuildSystemKind.Cargo,
            "Rust/Cargo",
            $"Found {Path.GetRelativePath(root, cargoFile)}.",
            [Need("Rust/Cargo", "cargo", "Install Rust from https://rustup.rs"), NeedMsvcBuildTools()],
            [
                new("cargo", "fetch", workingDirectory, "Download Rust crates", true),
                new("powershell", CargoBuildCommand("build --release"), workingDirectory, "Cargo release build"),
                new("powershell", RustArtifactCommand(), workingDirectory, "Collect Rust artifacts")
            ]);
    }

    private static string RustArtifactCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
        "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
        "$files = Get-ChildItem -Path 'target' -Include *.exe,*.dll,*.rlib,*.zip -File -Recurse -ErrorAction SilentlyContinue | " +
        "Where-Object { $_.FullName -notmatch '\\\\(deps|build|incremental)\\\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 20; " +
        "$count = 0; foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $artifactDir $file.Name) -Force; $count++ }; " +
        "if ($count -eq 0) { $cmd = Join-Path $artifactDir 'run-cargo-release.cmd'; Set-Content -LiteralPath $cmd -Value ('@echo off' + [Environment]::NewLine + 'cd /d \"' + (Get-Location).Path + '\"' + [Environment]::NewLine + 'cargo run --release %*' + [Environment]::NewLine) -Encoding ASCII; $count++ }; " +
        "Write-Output ('Prepared Rust artifact(s) in ' + $artifactDir)\"";

    private static string CargoBuildCommand(string cargoArguments) =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\\Installer\\vswhere.exe'; " +
        "$vsdev = $null; " +
        "if (Test-Path $vswhere) { $vsdev = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'Common7\\Tools\\VsDevCmd.bat' | Select-Object -First 1 }; " +
        $"if ($vsdev) {{ $bat = Join-Path $env:TEMP ('git-build-cargo-' + [Guid]::NewGuid().ToString('N') + '.cmd'); " +
        $"$lines = @('@echo off', 'call \"' + $vsdev + '\" -arch=x64 -host_arch=x64', 'cargo {cargoArguments}'); " +
        $"$exitCode = 0; Set-Content -LiteralPath $bat -Value $lines -Encoding ASCII; & $bat; $exitCode = $LASTEXITCODE; Remove-Item -LiteralPath $bat -Force -ErrorAction SilentlyContinue; exit $exitCode }} else {{ cargo {cargoArguments} }}; " +
        "exit $LASTEXITCODE\"";

    private static DetectionResult Go(string root, string goMod)
    {
        var workingDirectory = FolderOf(goMod, root);
        var mainPackages = FindGoMainPackages(workingDirectory).ToArray();
        var needsCgo = GoProjectMayNeedCgo(workingDirectory);
        var artifactDir = Path.Combine(workingDirectory, "Git-Build-Artifacts");
        var requirements = new List<DependencyRequirement>
        {
            Need("Go", "go", "Install Go from https://go.dev/dl/")
        };
        if (needsCgo)
        {
            requirements.Add(NeedMsvcBuildTools());
        }

        var commands = new List<CommandSpec>
        {
            new("go", "mod download", workingDirectory, "Download Go modules", true)
        };

        if (mainPackages.Length > 0)
        {
            Directory.CreateDirectory(artifactDir);
            foreach (var packageDir in mainPackages)
            {
                var relative = Path.GetRelativePath(workingDirectory, packageDir);
                var name = relative == "." ? new DirectoryInfo(workingDirectory).Name : relative.Replace(Path.DirectorySeparatorChar, '-').Replace(Path.AltDirectorySeparatorChar, '-');
                var outputName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
                var outputPath = Path.Combine(artifactDir, outputName);
                commands.Add(CreateGoBuildCommand(
                    needsCgo,
                    packageDir,
                    $"build -o \"{outputPath}\" .",
                    $"Go build executable {name}"));
            }
        }
        else
        {
            commands.Add(CreateGoBuildCommand(needsCgo, workingDirectory, "build ./...", "Go build packages"));
        }

        var reason = mainPackages.Length > 0
            ? $"Found go.mod and {mainPackages.Length} runnable Go package(s). Git-Build will create executable artifacts."
            : "Found go.mod. Git-Build will verify all Go packages build.";
        if (needsCgo)
        {
            reason += " Native GUI/OpenGL bindings were detected, so Git-Build will build with CGO enabled.";
        }

        return new DetectionResult(
            BuildSystemKind.Go,
            "Go",
            $"{reason} Found {Path.GetRelativePath(root, goMod)}.",
            requirements,
            commands);
    }

    private static CommandSpec CreateGoBuildCommand(bool needsCgo, string workingDirectory, string goArguments, string displayName) =>
        needsCgo && OperatingSystem.IsWindows()
            ? new CommandSpec("powershell", GoCgoBuildCommand(goArguments), workingDirectory, displayName)
            : new CommandSpec("go", goArguments, workingDirectory, displayName);

    private static string GoCgoBuildCommand(string goArguments) =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$env:CGO_ENABLED = '1'; " +
        "$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\\Installer\\vswhere.exe'; " +
        "$vsdev = $null; " +
        "if (Test-Path $vswhere) { $vsdev = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'Common7\\Tools\\VsDevCmd.bat' | Select-Object -First 1 }; " +
        $"if ($vsdev) {{ cmd /d /s /c ('call \"' + $vsdev + '\" -arch=x64 -host_arch=x64 && set CGO_ENABLED=1&& go {goArguments}') }} else {{ go {goArguments} }}; " +
        "exit $LASTEXITCODE\"";

    private static bool GoProjectMayNeedCgo(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.go", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}vendor{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Git-Build-Artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var text = string.Join('\n', File.ReadLines(file).Take(200));
                if (text.Contains("import \"C\"", StringComparison.Ordinal) ||
                    text.Contains("\"fyne.io/fyne/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\"github.com/go-gl/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore unreadable files and keep scanning the rest of the project.
            }
        }

        return false;
    }

    private static IEnumerable<string> FindGoMainPackages(string root)
    {
        return Directory.EnumerateFiles(root, "*.go", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}vendor{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Git-Build-Artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadLines(path).Take(40).Any(line => line.Trim().Equals("package main", StringComparison.OrdinalIgnoreCase)))
            .Where(path => !File.ReadLines(path).Take(10).Any(line => line.TrimStart().StartsWith("//go:build", StringComparison.OrdinalIgnoreCase) || line.TrimStart().StartsWith("// +build", StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetRelativePath(root, path!))!;
    }
    private static DetectionResult Ruby(string root, string gemfile)
    {
        var workingDirectory = FolderOf(gemfile, root);
        return new DetectionResult(
            BuildSystemKind.Ruby,
            "Ruby",
            $"Found {Path.GetRelativePath(root, gemfile)}.",
            [Need("Ruby", "ruby", "Install Ruby."), Need("Bundler", "bundle", "Install Bundler with gem install bundler.")],
            [
                new("gem", "install bundler", workingDirectory, "Install/upgrade Bundler", true),
                new("bundle", "install", workingDirectory, "Install Ruby gems", true),
                new("bundle", "exec rake build", workingDirectory, "Ruby build"),
                new("powershell", RubyArtifactCommand(), workingDirectory, "Collect Ruby artifacts")
            ]);
    }

    private static DetectionResult Docker(string root, string dockerfile)
    {
        var workingDirectory = FolderOf(dockerfile, root);
        return new DetectionResult(
            BuildSystemKind.Docker,
            "Docker",
            $"Found {Path.GetRelativePath(root, dockerfile)}.",
            [Need("Docker", "docker", "Install Docker Desktop.")],
            [
                new("docker", "build --pull -t git-build-output .", workingDirectory, "Docker build", true),
                new("powershell", DockerLauncherCommand(), workingDirectory, "Create Docker launcher")
            ]);
    }

    private static DetectionResult Make(string root, string makefile)
    {
        var workingDirectory = FolderOf(makefile, root);
        return new DetectionResult(
            BuildSystemKind.Make,
            "Make",
            $"Found {Path.GetRelativePath(root, makefile)}.",
            [Need("Make", "make", "Install Make or use a compatible developer environment.")],
            [
                new("make", "", workingDirectory, "Make build"),
                new("powershell", CollectArtifactsCommand(".", "*.exe", "*.dll", "*.so", "*.dylib", "*.a", "*.lib", "*.zip", "*.tar", "*.gz"), workingDirectory, "Collect Make artifacts")
            ]);
    }

    private static string CollectArtifactsCommand(string searchRoot, params string[] patterns)
    {
        var escapedSearchRoot = searchRoot.Replace("'", "''");
        var patternList = string.Join(",", patterns.Select(pattern => $"'{pattern.Replace("'", "''")}'"));
        return "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
            "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
            $"$searchRoot = Join-Path (Get-Location) '{escapedSearchRoot}'; " +
            "if (!(Test-Path $searchRoot)) { $searchRoot = Get-Location }; " +
            $"$patterns = @({patternList}); " +
            "$copied = 0; " +
            "foreach ($pattern in $patterns) { Get-ChildItem -Path $searchRoot -Filter $pattern -File -Recurse -ErrorAction SilentlyContinue | " +
            "Where-Object { $_.FullName -notmatch '\\\\(obj|node_modules|\\.git|Git-Build-Artifacts)\\\\' } | " +
            "Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 20 | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $artifactDir $_.Name) -Force; $copied++ } }; " +
            "if ($copied -eq 0) { Write-Output 'No package files were found to collect.' } else { Write-Output ('Collected ' + $copied + ' artifact file(s) in ' + $artifactDir) }\"";
    }

    private static string NodeArtifactCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
        "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
        "$names = @('dist','build','out','release','public'); $created = 0; " +
        "foreach ($name in $names) { if (Test-Path $name) { $zip = Join-Path $artifactDir ($name + '.zip'); if (Test-Path $zip) { Remove-Item $zip -Force }; Compress-Archive -Path (Join-Path $name '*') -DestinationPath $zip -Force; $created++ } }; " +
        "Get-ChildItem -Path . -Include *.exe,*.msi,*.zip,*.tar,*.gz -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\\\(node_modules|\\.git|Git-Build-Artifacts)\\\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 20 | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $artifactDir $_.Name) -Force; $created++ }; " +
        "if ($created -eq 0) { Set-Content -LiteralPath (Join-Path $artifactDir 'open-project.cmd') -Value ('@echo off' + [Environment]::NewLine + 'cd /d \"' + (Get-Location).Path + '\"' + [Environment]::NewLine + 'npm start' + [Environment]::NewLine) -Encoding ASCII; $created++ }; " +
        "Write-Output ('Prepared Node artifact(s) in ' + $artifactDir)\"";

    private static string RubyArtifactCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
        "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
        "$files = Get-ChildItem -Path . -Include *.gem,*.exe,*.zip,*.tar,*.gz -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\\\(vendor|\\.git|Git-Build-Artifacts)\\\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 20; " +
        "$count = 0; foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $artifactDir $file.Name) -Force; $count++ }; " +
        "if ($count -eq 0) { Set-Content -LiteralPath (Join-Path $artifactDir 'ruby-build.cmd') -Value ('@echo off' + [Environment]::NewLine + 'cd /d \"' + (Get-Location).Path + '\"' + [Environment]::NewLine + 'bundle exec rake build' + [Environment]::NewLine) -Encoding ASCII }; " +
        "Write-Output ('Prepared Ruby artifact(s) in ' + $artifactDir)\"";

    private static string DockerLauncherCommand() =>
        "-NoProfile -ExecutionPolicy Bypass -Command \"$artifactDir = Join-Path (Get-Location) 'Git-Build-Artifacts'; " +
        "New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null; " +
        "$cmd = Join-Path $artifactDir 'run-docker-image.cmd'; " +
        "Set-Content -LiteralPath $cmd -Value ('@echo off' + [Environment]::NewLine + 'docker run --rm -it git-build-output %*' + [Environment]::NewLine) -Encoding ASCII; " +
        "Write-Output ('Created Docker launcher ' + $cmd)\"";

    private static DetectionResult Unknown() => new(
        BuildSystemKind.Unknown, "Unknown", "Git-Build could not identify a supported build file.",
        [], []);
}





