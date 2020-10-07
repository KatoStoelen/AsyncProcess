using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenNonZeroExitCode
{
    public class WhenProcessIsCancelledAfterCompletion : GivenWhenThenTest
    {
        private ProcessRunner _runner;
        private CancellationTokenSource _cts;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner()
                .WithArguments(args => args.AddOption("--duration", "1"));

            _cts = new CancellationTokenSource();
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync(_cts.Token);

            _cts.CancelAfter(TimeSpan.FromSeconds(2));
        }

        [Then]
        public void TaskShouldNotThrowException()
        {
            Assert.DoesNotThrowAsync(() => _runTask);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cts?.Dispose();
        }
    }
}