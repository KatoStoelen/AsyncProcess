using System;
#if NETSTANDARD2_0
using System.Collections.Generic;
#endif
using System.Diagnostics;
#if NETSTANDARD2_0
using System.IO;
using System.Runtime.InteropServices;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace AsyncProcess.Internal.Extensions
{
    // Slightly modified version of:
    // https://github.com/dotnet/cli/blob/master/test/Microsoft.DotNet.Tools.Tests.Utilities/Extensions/ProcessExtensions.cs

    /// <summary>
    /// Extensions of <see cref="Process"/>.
    /// </summary>
    public static class ProcessExtensions
    {
#if NETSTANDARD2_0
        private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(30);

        internal static void KillTree(this Process process)
        {
            process.KillTree(s_defaultTimeout);
        }

        internal static void KillTree(this Process process, TimeSpan timeout)
        {
            if (s_isWindows)
            {
                RunProcessAndWaitForExit(
                    "taskkill",
                    $"/T /F /PID {process.Id}",
                    timeout,
                    out _);
            }
            else
            {
                var children = new HashSet<int>();
                GetAllChildIdsUnix(process.Id, children, timeout);
                foreach (var childId in children)
                {
                    KillProcessUnix(childId, timeout);
                }
                KillProcessUnix(process.Id, timeout);
            }
        }

        private static void GetAllChildIdsUnix(int parentId, ISet<int> children, TimeSpan timeout)
        {
            var exitCode = RunProcessAndWaitForExit(
                "pgrep",
                $"-P {parentId}",
                timeout,
                out var stdout);

            if (exitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                using var reader = new StringReader(stdout);

                while (true)
                {
                    var text = reader.ReadLine();
                    if (text == null)
                    {
                        return;
                    }

                    if (int.TryParse(text, out var id))
                    {
                        children.Add(id);
                        // Recursively get the children
                        GetAllChildIdsUnix(id, children, timeout);
                    }
                }
            }
        }

        private static void KillProcessUnix(int processId, TimeSpan timeout)
        {
            RunProcessAndWaitForExit(
                "kill",
                $"-TERM {processId}",
                timeout,
                out _);
        }
#endif

        internal static int RunProcessAndWaitForExit(
            string fileName, string arguments, TimeSpan timeout, out string? stdout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);

            stdout = null;
            if (process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                stdout = process.StandardOutput.ReadToEnd();
            }
            else
            {
                process.Kill();
            }

            return process.ExitCode;
        }

        /// <summary>
        /// Starts the specified <see cref="Process"/> and returns a task that
        /// completes when process exits.
        /// </summary>
        /// <remarks>
        /// Requesting cancellation only cancels the returned task. No attempts
        /// are made to stop or kill the process.
        /// </remarks>
        /// <param name="process">The process to start and wait for exit.</param>
        /// <param name="cancellationToken">
        /// An optional cancellation token to cancel the returned task.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If the specified process fails to start.
        /// </exception>
        /// <returns>A task that completes when the process exits.</returns>
        public static async Task<int> StartAndWaitForExitAsync(
            this Process process, CancellationToken cancellationToken = default)
        {
            var processCompletionSource = new TaskCompletionSource<int>();

            process.EnableRaisingEvents = true;
            process.Exited += OnExited;

            using var _ = cancellationToken.Register(() =>
            {
                process.Exited -= OnExited;
                processCompletionSource.SetCanceled();
            });

            return process.Start()
                ? await processCompletionSource.Task
                : throw new ArgumentException("Failed to start specified process", nameof(process));

            void OnExited(object? sender, EventArgs e)
            {
                process.Exited -= OnExited;

                processCompletionSource.SetResult(process.ExitCode);
            }
        }
    }
}