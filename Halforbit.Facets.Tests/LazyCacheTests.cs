using Halforbit.Facets.Implementation;
using System;
using Xunit;

namespace Halforbit.Facets.Tests
{
    public class LazyCacheTests
    {
        [Fact, Trait("Type", "Unit")]
        public void Test()
        {
            var instance = LazyCache<ITestType>.Create(() => new TestType());

            var message1 = instance.Message;

            var message2 = instance.Message;

            Assert.StartsWith("Test ", message1);

            Assert.Equal(message1, message2);
        }

        //[Fact, Trait("Type", "Unit")] // Not supported
        public void Test2()
        {
            var instance = LazyCache<TestType2>.Create(() => new TestType2());

            var message1 = instance.Message;

            var message2 = instance.Message;

            Assert.StartsWith("Test ", message1);

            Assert.Equal(message1, message2);
        }
    }

    public interface ITestType
    {
        string Message { get; }
    }

    public class TestType : ITestType
    {
        readonly Random _random = new Random(0);

        public string Message => $"Test {_random.Next()}";
    }

    public class TestType2
    {
        readonly Random _random = new Random(0);

        public virtual string Message => $"Test {_random.Next()}";
    }
}
