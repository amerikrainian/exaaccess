using System;
using System.Linq;
using ExaAccess;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>
    /// The ordinal contract these tests pin down: members come back in MetadataToken order, which for
    /// a compiled type is declaration order — the same order de4dot's method_N/field_N names index.
    /// </summary>
    public class MemberResolverTests
    {
        // Declaration order is deliberately scrambled across visibility/static-ness so the tests prove
        // token order, not GetMethods' default (which groups public first). The fields exist only to
        // be enumerated — never assigned.
#pragma warning disable 0649
        private sealed class Fixture
        {
            public void Alpha() { }
            private static int Bravo() => 0;
            internal string Charlie(int x) => x.ToString();
            public static void Delta(string s) { }

            public int FieldOne;
            private static string _fieldTwo;
            internal double FieldThree;

            // Silence "never used" noise.
            public override string ToString() => Bravo() + _fieldTwo + FieldOne + FieldThree;
        }
#pragma warning restore 0649

        [Fact]
        public void MethodsComeBackInDeclarationOrder()
        {
            var names = MemberResolver.MethodsInTokenOrder(typeof(Fixture)).Select(m => m.Name).ToArray();
            Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "ToString" }, names);
        }

        [Fact]
        public void FieldsComeBackInDeclarationOrder()
        {
            var names = MemberResolver.FieldsInTokenOrder(typeof(Fixture)).Select(f => f.Name).ToArray();
            Assert.Equal(new[] { "FieldOne", "_fieldTwo", "FieldThree" }, names);
        }

        [Fact]
        public void MethodByOrdinalSelectsTheNthMethod()
        {
            Assert.Equal("Charlie", MemberResolver.MethodByOrdinal(typeof(Fixture), 2, "test").Name);
        }

        [Fact]
        public void MethodByOrdinalOutOfRangeIsNullNotThrow()
        {
            Assert.Null(MemberResolver.MethodByOrdinal(typeof(Fixture), 99, "test"));
            Assert.Null(MemberResolver.MethodByOrdinal(typeof(Fixture), -1, "test"));
        }

        [Fact]
        public void FieldByOrdinalSelectsAndRangeChecks()
        {
            Assert.Equal("_fieldTwo", MemberResolver.FieldByOrdinal(typeof(Fixture), 1).Name);
            Assert.Null(MemberResolver.FieldByOrdinal(typeof(Fixture), 3));
        }

        [Fact]
        public void DescribeRendersSignatureAndHandlesNull()
        {
            var m = MemberResolver.MethodByOrdinal(typeof(Fixture), 2, "test");
            string s = MemberResolver.Describe(m);
            Assert.StartsWith("String #", s);
            Assert.EndsWith("(Int32)", s);
            Assert.Equal("<null>", MemberResolver.Describe(null));
        }
    }
}
