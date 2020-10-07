using System;

namespace AsyncProcess.Tests
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class TestProcessAttribute : Attribute
    {
        public TestProcessAttribute(string assemblyPath)
        {
            AssemblyPath = assemblyPath;
        }

        public string AssemblyPath { get; }
    }
}