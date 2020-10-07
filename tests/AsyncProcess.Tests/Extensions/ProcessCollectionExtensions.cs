using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AsyncProcess.Internal.Extensions;

namespace AsyncProcess.Tests.Extensions
{
    internal static class ProcessCollectionExtensions
    {
        private const string TestProcessDllName = "TestProcess.dll";

        public static IEnumerable<Process> WhereIsTestProcess(
            this ICollection<Process> processes)
        {
            if (processes == null)
            {
                throw new ArgumentNullException(nameof(processes));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return processes.WhereContainsModule(TestProcessDllName);
            }
            else
            {
                return UnixEnumerator();
            }

            IEnumerable<Process> UnixEnumerator()
            {
                foreach (var process in processes)
                {
                    string commandLineArgs = null;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        commandLineArgs = process.GetCommandLineArgsLinux();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        commandLineArgs = process.GetCommandLineArgsMacOS();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unsupported OS: {RuntimeInformation.OSDescription}");
                    }

                    if (commandLineArgs?.Contains(TestProcessDllName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        yield return process;
                    }
                }
            }
        }

        private static IEnumerable<Process> WhereContainsModule(
            this ICollection<Process> processes, string moduleName)
        {
            foreach (var process in processes)
            {
                var containsModule = false;

                try
                {
                    containsModule = (
                        from ProcessModule module in process.Modules
                        where module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase)
                        select true
                    ).Any();
                }
                catch { }

                if (containsModule)
                {
                    yield return process;
                }
            }
        }

        private static string GetCommandLineArgsLinux(this Process process)
        {
            return File.ReadAllText($"/proc/{process.Id}/cmdline");
        }

        private static string GetCommandLineArgsMacOS(this Process process)
        {
            var exitCode = ProcessExtensions.RunProcessAndWaitForExit(
                "ps",
                $"-p {process.Id} -o args",
                TimeSpan.FromSeconds(10),
                out var output);

            return exitCode == 0
                ? output
                : throw new Exception($"Failed to invoke 'ps -p {process.Id} -o args' (exit code {exitCode})");
        }
    }
}