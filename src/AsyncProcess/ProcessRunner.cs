using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncProcess.Internal.Extensions;

namespace AsyncProcess
{
    /// <summary>
    /// The asynchronous process runner.
    /// </summary>
    public class ProcessRunner
    {
        /// <summary>
        /// Initializes a process runner.
        /// </summary>
        /// <param name="fileName">The file name of the executable.</param>
        /// <exception cref="ArgumentException">
        /// If the file name is null, empty or whitespace-only.
        /// </exception>
        public ProcessRunner(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name must be set.");
            }

            FileName = fileName;
        }

        /// <summary>
        /// The file name of the executable.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The (optional) working directory of the process.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// The (optional) process arguments.
        /// </summary>
        public ProcessArguments? Arguments { get; set; }

        /// <summary>
        /// Whether or not to write the process output to <see cref="Trace"/>.
        /// <para>
        /// Defaults to <see langword="false"/>.
        /// </para>
        /// </summary>
        public bool ShouldWriteToTrace { get; set; }

        /// <summary>
        /// Sets the working directory of the process.
        /// </summary>
        /// <param name="workingDirectory">The path to the working directory.</param>
        /// <returns>
        /// The current <see cref="ProcessRunner"/> instance for chaining purposes.
        /// </returns>
        public ProcessRunner WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;

            return this;
        }

        /// <summary>
        /// Sets the process arguments.
        /// </summary>
        /// <param name="configure">A delegate configuring the process arguments.</param>
        /// <returns>
        /// The current <see cref="ProcessRunner"/> instance for chaining purposes.
        /// </returns>
        public ProcessRunner WithArguments(Action<ProcessArguments> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            Arguments ??= new ProcessArguments();

            configure(Arguments);

            return this;
        }

        /// <summary>
        /// Sets the process arguments.
        /// </summary>
        /// <param name="arguments">The process arguments.</param>
        /// <returns>
        /// The current <see cref="ProcessRunner"/> instance for chaining purposes.
        /// </returns>
        public ProcessRunner WithArguments(ProcessArguments arguments)
        {
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

            return this;
        }

        /// <summary>
        /// Sets whether or not to write the process output to <see cref="Trace"/>.
        /// <para>
        /// Defaults to <see langword="false"/>.
        /// </para>
        /// </summary>
        /// <param name="writeToTrace"></param>
        /// <returns>
        /// The current <see cref="ProcessRunner"/> instance for chaining purposes.
        /// </returns>
        public ProcessRunner WriteToTrace(bool writeToTrace)
        {
            ShouldWriteToTrace = writeToTrace;

            return this;
        }

        /// <summary>
        /// Runs the process asynchronously.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The exit code of the process.</returns>
        public Task<int> RunAsync(
                CancellationToken cancellationToken = default) =>
            RunAsync(TextWriter.Null, cancellationToken);

        /// <summary>
        /// Runs the process asynchronously.
        /// </summary>
        /// <param name="outputWriter">
        /// A <see cref="TextWriter"/> to capture all process output.
        /// <para>
        /// Both Standard Out (STDOUT) and Standard Error (STDERR).
        /// </para>
        /// </param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The exit code of the process.</returns>
        public Task<int> RunAsync(
                TextWriter outputWriter,
                CancellationToken cancellationToken = default) =>
            RunAsync(outputWriter, outputWriter, cancellationToken);

        /// <summary>
        /// Runs the process asynchronously.
        /// </summary>
        /// <param name="stdOutWriter">
        /// A <see cref="TextWriter"/> to capture Standard Out (STDOUT).
        /// </param>
        /// <param name="stdErrWriter">
        /// A <see cref="TextWriter"/> to capture Standard Error (STDERR).
        /// </param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The exit code of the process.</returns>
        public async Task<int> RunAsync(
            TextWriter stdOutWriter,
            TextWriter stdErrWriter,
            CancellationToken cancellationToken = default)
        {
            if (stdOutWriter == null)
            {
                throw new ArgumentNullException(nameof(stdOutWriter));
            }

            if (stdErrWriter == null)
            {
                throw new ArgumentNullException(nameof(stdErrWriter));
            }

            var processStartInfo = new ProcessStartInfo(FileName)
            {
#if NETSTANDARD2_0
                Arguments = Arguments,
#endif
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

#if NETCOREAPP3_1
            foreach (var arg in Arguments ?? Enumerable.Empty<string>())
            {
                processStartInfo.ArgumentList.Add(arg);
            }
#endif

            if (!string.IsNullOrEmpty(WorkingDirectory))
            {
                processStartInfo.WorkingDirectory = WorkingDirectory;
            }

            using var process = new Process { StartInfo = processStartInfo };
            var outputConsumerThreads = new List<Thread>(2);

            try
            {
                var runTask = process.StartAndWaitForExitAsync(cancellationToken);

                if (stdOutWriter != TextWriter.Null)
                {
                    outputConsumerThreads.Add(Consume(process.StandardOutput, stdOutWriter));
                }

                if (stdErrWriter != TextWriter.Null)
                {
                    outputConsumerThreads.Add(Consume(process.StandardError, stdErrWriter));
                }

                await runTask;
            }
            catch
            {
                if (!process.HasExited)
                {
#if NETSTANDARD2_0
                    process.KillTree();
#else
                    process.Kill(entireProcessTree: true);
#endif
                }

                throw;
            }
            finally
            {
                outputConsumerThreads.ForEach(thread => thread.Join());
            }

            return process.ExitCode;
        }

        private Thread Consume(TextReader reader, TextWriter writer)
        {
            var thread = new Thread(() =>
            {
                int readCharacter;
                while ((readCharacter = reader.Read()) != -1)
                {
                    writer.Write((char)readCharacter);
                }

                writer.Flush();
            });

            thread.Start();

            return thread;
        }

        /// <summary>
        /// Converts the process runner to a string format.
        /// <para>
        /// Format: [File Name] [Arguments]
        /// </para>
        /// </summary>
        /// <returns>A string representation of the process runner.</returns>
        public override string ToString() => $"{FileName} {Arguments}".Trim();
    }
}