using System;
using System.Reflection;

namespace Echopunks
{
    /// <summary>
    /// The token-ordinal member resolution primitive, extracted from <see cref="GameState"/> so the
    /// mechanism is game-agnostic and unit-testable. The contract (see CLAUDE.md "Analysis workspace"):
    /// de4dot does not preserve metadata tokens but DOES preserve each type's member ORDER, so de4dot's
    /// <c>method_N</c> is the N-th declared method in MetadataToken order and the same ordinal selects
    /// the same member on the shipping (obfuscated) exe. Callers must still cross-check what comes back
    /// (signature, field type) so a game update fails loudly — helpers for that live here too.
    /// </summary>
    internal static class MemberResolver
    {
        public const BindingFlags AllDeclared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>All declared methods of <paramref name="type"/> in metadata-token order — the order
        /// de4dot's <c>method_N</c> ordinals index into.</summary>
        public static MethodInfo[] MethodsInTokenOrder(Type type)
        {
            var methods = type.GetMethods(AllDeclared);
            Array.Sort(methods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return methods;
        }

        /// <summary>All declared fields of <paramref name="type"/> in metadata-token order.</summary>
        public static FieldInfo[] FieldsInTokenOrder(Type type)
        {
            var fields = type.GetFields(AllDeclared);
            Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return fields;
        }

        /// <summary>The method at de4dot ordinal <paramref name="ordinal"/>, or null (logged) when out of
        /// range. The caller owns signature cross-checks — what "expected" looks like is per-member.</summary>
        public static MethodInfo MethodByOrdinal(Type type, int ordinal, string role)
        {
            var methods = MethodsInTokenOrder(type);
            if (ordinal < 0 || ordinal >= methods.Length)
            {
                Log.Error("[resolve] " + role + " method ordinal " + ordinal + " out of range (" + methods.Length + " methods on " + type.Name + ").");
                return null;
            }
            return methods[ordinal];
        }

        /// <summary>The field at de4dot ordinal <paramref name="ordinal"/>, or null when out of range.</summary>
        public static FieldInfo FieldByOrdinal(Type type, int ordinal)
        {
            var fields = FieldsInTokenOrder(type);
            return (ordinal >= 0 && ordinal < fields.Length) ? fields[ordinal] : null;
        }

        /// <summary>Human-readable method signature for logs: return type, metadata token, param types.</summary>
        public static string Describe(MethodInfo m)
        {
            if (m == null) return "<null>";
            var ps = m.GetParameters();
            var names = new string[ps.Length];
            for (int i = 0; i < ps.Length; i++) names[i] = ps[i].ParameterType.Name;
            return m.ReturnType.Name + " #" + m.MetadataToken.ToString("X") + "(" + string.Join(",", names) + ")";
        }
    }
}
