using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenNonZeroExitCode
{
    public class WhenRunningTheProcess : GivenWhenThenTest
    {
        private ProcessRunner _runner;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner()
                .WithArguments(args => args.AddOption("--exit-code", "4"));
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync();
        }

        [Then]
        public async Task TheNonZeroExitCodeShouldBeReturned()
        {
            var exitCode = await _runTask;

            Assert.AreEqual(4, exitCode);
        }
    }
}