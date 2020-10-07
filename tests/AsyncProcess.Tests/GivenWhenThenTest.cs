using NUnit.Framework;

namespace AsyncProcess.Tests
{
    public abstract class GivenWhenThenTest
    {
        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            Given();
            When();
        }

        protected abstract void Given();
        protected abstract void When();
    }

    public class ThenAttribute : TestAttribute
    {
    }
}