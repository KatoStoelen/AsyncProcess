using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AsyncProcess.Tests.Extensions;
using NUnit.Framework;

namespace AsyncProcess.Tests.GivenCancellation
{
    public class WhenProcessIsCancelledBeforeCompletion : GivenWhenThenTest
    {
        private readonly TimeSpan _cancellationDelay = TimeSpan.FromSeconds(3);
        private ProcessRunner _runner;
        private CancellationTokenSource _cts;
        private Task<int> _runTask;

        protected override void Given()
        {
            _runner = TestProcess.CreateProcessRunner()
                .WithArguments(args => args.AddOption("--duration", "10"));

            _cts = new CancellationTokenSource();
        }

        protected override void When()
        {
            _runTask = _runner.RunAsync(_cts.Token);

            _cts.CancelAfter(_cancellationDelay);
        }

        [Then]
        public void ProcessShouldBeStarted()
        {
            var testProcesses = Process.GetProcessesByName("dotnet")
                .WhereIsTestProcess();

            Assert.That(testProcesses, Is.Not.Empty);
        }

        [Then]
        public void TaskShouldThrowTaskCanceledException()
        {
            Assert.ThrowsAsync<TaskCanceledException>(() => _runTask);
        }

        [Then]
        public async Task ProcessShouldBeStopped()
        {
            // Wait for cancellation + small grace period
            await Task.Delay(_cancellationDelay.Add(TimeSpan.FromMilliseconds(500)));

            var testProcesses = Process.GetProcessesByName("dotnet")
                .WhereIsTestProcess();

            Assert.That(testProcesses, Is.Empty);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cts?.Dispose();
        }
    }
}