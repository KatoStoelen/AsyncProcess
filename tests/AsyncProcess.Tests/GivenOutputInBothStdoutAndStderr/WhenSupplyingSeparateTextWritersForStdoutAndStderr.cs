using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenOutputInBothStdoutAndStderr
{
    public class WhenSupplyingSeparateTextWritersForStdoutAndStderr : GivenWhenThenTest
    {
        private ProcessRunner _runner;
        private StringWriter _stdoutWriter;
        private StringWriter _stderrWriter;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner()
                .WithArguments(args =>
                    args
                        .AddOption("--stdout", "EXPECTED STDOUT")
                        .AddOption("--stderr", "EXPECTED STDERR"));

            _stdoutWriter = new StringWriter();
            _stderrWriter = new StringWriter();
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync(_stdoutWriter);
        }

        [Then]
        public async Task TextWriterForStdoutShouldContainStdoutOutput()
        {
            await _runTask;

            var standardOutput = _stdoutWriter.ToString();

            Assert.That(standardOutput, Contains.Substring("EXPECTED STDOUT"));
        }

        [Then]
        public async Task TextWriterForStderrShouldContainStderrOutput()
        {
            await _runTask;

            var standardError = _stdoutWriter.ToString();

            Assert.That(standardError, Contains.Substring("EXPECTED STDERR"));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _stdoutWriter?.Dispose();
            _stderrWriter?.Dispose();
        }
    }
}