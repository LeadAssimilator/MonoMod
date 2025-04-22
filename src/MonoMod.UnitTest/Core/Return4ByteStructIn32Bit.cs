using MonoMod.Core;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace MonoMod.UnitTest.Core
{
    [Obsolete("This test is replaced by DetouringMethodsReturningStructs")]
    public class Return4ByteStructIn32Bit : TestBase
    {
        public Return4ByteStructIn32Bit(ITestOutputHelper helper) : base(helper)
        {
        }

        private struct St04
        {
            public byte b1;
            public byte b2;
            public byte b3;
            public byte b4;
        }

        private class Clazz
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public St04 Original(string s)
            {
                Console.WriteLine("Original should never be called (" + s + ")");
                return new St04();
            }

            public static St04 Replacement(Clazz _, string s)
            {
                Console.WriteLine("Replacement called with " + s);
                return new St04();
            }
        }

        [Fact]
        public void Returning4ByteStructInInstanceMethodWithParameters_DoesNotThrow()
        {
            var all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var method = typeof(Clazz).GetMethod(nameof(Clazz.Original), all);
            var replacement = typeof(Clazz).GetMethod(nameof(Clazz.Replacement), all);
            using var result = DetourFactory.Current.CreateDetour(method, replacement);
            _ = (new Clazz()).Original("test");
        }
    }
}
