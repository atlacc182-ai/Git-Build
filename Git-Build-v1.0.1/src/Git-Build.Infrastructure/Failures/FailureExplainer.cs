using GitBuild.Core.Models;
using GitBuild.Core.Services;

namespace GitBuild.Infrastructure.Failures;

public sealed class FailureExplainer : IFailureExplainer
{
    public BuildFailureExplanation Explain(string logText)
    {
        var lower = logText.ToLowerInvariant();
        var causes = new List<string>();
        var fixes = new List<string>();

        AddIf(lower.Contains("command not found") || lower.Contains("is not recognized"), "A required command-line tool is missing.", "Install the missing tool, then restart Git-Build so it can see the updated PATH.");
        AddIf(lower.Contains("permission denied") || lower.Contains("access is denied"), "The build could not access a file or folder.", "Move the repository to a writable folder or run Git-Build with appropriate permissions.");
        AddIf(lower.Contains("could not resolve") || lower.Contains("network") || lower.Contains("timed out"), "The build could not reach a package source or remote server.", "Check your network connection, proxy, VPN, and package registry access.");
        AddIf(lower.Contains("no such file") || lower.Contains("cannot find"), "A file expected by the build is missing.", "Check the repository instructions for generated files, submodules, or setup steps.");
        AddIf(lower.Contains("test failed") || lower.Contains("failed tests"), "The project built far enough to run tests, but at least one test failed.", "Inspect the first failing test in the log and decide whether to fix code or build without tests.");
        AddIf(lower.Contains("eftype") && lower.Contains("esbuild"), "The esbuild native binary is corrupted or the wrong Windows executable was installed.", "Delete the affected esbuild folders in node_modules, then run npm install again. Git-Build now tries this repair automatically before reinstalling npm packages.");
        AddIf(lower.Contains("cannot find module") && lower.Contains("node_modules"), "The node_modules folder is incomplete. A previous npm install likely failed before all build tools were installed.", "Run npm install again, or delete node_modules and let Git-Build reinstall dependencies.");
        AddIf(lower.Contains("node-gyp") && (lower.Contains("could not find any visual studio") || lower.Contains("desktop development with c++") || lower.Contains("gyp err! find vs")), "Visual Studio C++ Build Tools are missing. This Node project has a native dependency that must compile C/C++ code.", "Install Visual Studio Build Tools 2022 with the Desktop development with C++ workload, then restart Git-Build and build again.");
        AddIf(lower.Contains("eresolve") || lower.Contains("unable to resolve dependency tree"), "npm could not resolve the project dependency tree because two packages require incompatible versions.", "Run npm install with --legacy-peer-deps, or update the conflicting packages in package.json.");
        AddIf(lower.Contains("nuget") || lower.Contains("npm err") || lower.Contains("npm error") || lower.Contains("pip") || lower.Contains("maven") || lower.Contains("gradle"), "A package restore or dependency step failed.", "Retry after clearing the package manager cache, or install the dependency listed near the first error.");

        if (causes.Count == 0)
        {
            causes.Add("The build command returned a non-zero exit code.");
            fixes.Add("Start with the first error line in the log. Later messages are often follow-on noise.");
        }

        return new BuildFailureExplanation("Git-Build found the most likely reason from the build log.", causes, fixes);

        void AddIf(bool condition, string cause, string fix)
        {
            if (!condition)
            {
                return;
            }

            causes.Add(cause);
            fixes.Add(fix);
        }
    }
}
