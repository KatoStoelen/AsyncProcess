using System;
using System.Linq;
using System.Reflection;

namespace AsyncProcess.Tests
{
    internal static class TestProcess
    {
        private static readonly TestProcessAttribute s_testProcessAttribute =
            Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes<TestProcessAttribute>()
                .SingleOrDefault();

        public static ProcessRunner CreateProcessRunner()
        {
            if (s_testProcessAttribute == null)
            {
                throw new InvalidOperationException($"Missing TestProcess reference");
            }

            return new ProcessRunner("dotnet")
                .WithArguments(args => args.AddNoun(s_testProcessAttribute.AssemblyPath));
        }
    }
}