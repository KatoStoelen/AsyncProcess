using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenExitCodeZero
{
    public class WhenRunningTheProcess : GivenWhenThenTest
    {
        private ProcessRunner _runner;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner();
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync();
        }

        [Then]
        public async Task ExitCodeZeroShouldBeReturned()
        {
            var exitCode = await _runTask;

            Assert.AreEqual(0, exitCode);
        }
    }
}