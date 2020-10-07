using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenOutputInBothStdoutAndStderr
{
    public class WhenSupplyingATextWriterToCaptureAllProcessOutput : GivenWhenThenTest
    {
        private ProcessRunner _runner;
        private StringWriter _writer;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner()
                .WithArguments(args =>
                    args
                        .AddOption("--stdout", "EXPECTED STDOUT")
                        .AddOption("--stderr", "EXPECTED STDERR"));

            _writer = new StringWriter();
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync(_writer);
        }

        [Then]
        public async Task SuppliedTextWriterShouldContainOutputFromBothStdoutAndStderr()
        {
            await _runTask;

            var processOutput = _writer.ToString();

            Assert.That(processOutput, Contains.Substring("EXPECTED STDOUT"));
            Assert.That(processOutput, Contains.Substring("EXPECTED STDERR"));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _writer?.Dispose();
        }
    }
}