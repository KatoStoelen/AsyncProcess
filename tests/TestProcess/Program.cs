using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace TestProcess
{
    internal class Program
    {
        private static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Console application to test running of processes")
            {
                new Option<string>("--stdout", "Text to write to STDOUT"),
                new Option<string>("--stderr", "Text to write to STDERR"),
                new Option<int?>("--duration", "The number of seconds the process will run. Returns immediately if not specified."),
                new Option<int>("--exit-code", () => 0, "Exit code to return")
            };

            rootCommand.Handler = CommandHandler.Create<string, string, int?, int>(
                async (stdout, stderr, duration, exitCode) =>
                {
                    if (stdout != null)
                    {
                        Console.Out.WriteLine(stdout);
                    }

                    if (stderr != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(stderr);
                        Console.ResetColor();
                    }

                    if (duration.HasValue)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(duration.Value));
                    }

                    return exitCode;
                });

            return rootCommand.InvokeAsync(args);
        }
    }
}
