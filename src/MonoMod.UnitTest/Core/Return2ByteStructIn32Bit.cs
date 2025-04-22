using MonoMod.Core;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace MonoMod.UnitTest.Core
{
    [Obsolete("This test is replaced by DetouringMethodsReturningStructs")]
    public class Return2ByteStructIn32Bit : TestBase
    {
        public Return2ByteStructIn32Bit(ITestOutputHelper helper) : base(helper)
        {
        }

        private struct St02
        {
            public byte b1;
            public byte b2;
        }

        private class Clazz
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public St02 Original(string s)
            {
                Console.WriteLine("Original should never be called (" + s + ")");
                return new St02();
            }

            public static St02 Replacement(Clazz _, string s)
            {
                Console.WriteLine("Replacement called with " + s);
                return new St02();
            }
        }

        [Fact]
        public void Returning2ByteStructInInstanceMethodWithParameters_DoesNotThrow()
        {
            var all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var method = typeof(Clazz).GetMethod(nameof(Clazz.Original), all);
            var replacement = typeof(Clazz).GetMethod(nameof(Clazz.Replacement), all);
            using var result = DetourFactory.Current.CreateDetour(method, replacement);
            _ = (new Clazz()).Original("test");
        }
    }
}
