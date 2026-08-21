#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ExaAccess.Dev
{
    /// <summary>
    /// The /gui endpoint's engine: a reflection dump of the live screen model — the ExaAccess
    /// equivalent of WrathAccess's Unity-object dumps, tuned for the obfuscation workflow. Fields
    /// print in METADATA-TOKEN ORDER with their ordinal (`f[N]`), so every line lines up with de4dot's
    /// `..._N` names and with GameState-style ordinal resolution. Values render shallowly; nested
    /// mod-defined/game objects expand one level; collections show their first elements. Main thread
    /// only (the dev server routes it through the tick pump).
    /// </summary>
    internal static class ObjectDumper
    {
        private const int MaxCollectionItems = 24;
        private const int MaxLines = 400;

        /// <summary>The whole screen stack shallowly + the active screen in depth.</summary>
        public static string DumpActiveScreen()
        {
            if (!GameState.Bound) return "[not bound] GameState hasn't resolved GameLogic yet\n";
            var sb = new StringBuilder();

            var stack = GameState.ScreenStack();
            if (stack == null || stack.Count == 0) return "(no screens on the stack yet)\n";
            sb.Append("stack (bottom -> top):\n");
            for (int i = 0; i < stack.Count; i++)
                sb.Append("  ").Append(i).Append(": ").Append(stack[i]?.GetType().Name ?? "<null>").Append('\n');

            var screen = stack[stack.Count - 1];
            sb.Append('\n');
            Dump(sb, screen, "active", 0, 2, new HashSet<object>(ReferenceComparer.Instance));
            return sb.ToString();
        }

        private static void Dump(StringBuilder sb, object obj, string label, int indent, int depth, HashSet<object> seen)
        {
            if (Lines(sb) > MaxLines) return;
            string pad = new string(' ', indent * 2);
            if (obj == null) { sb.Append(pad).Append(label).Append(" = null\n"); return; }

            var t = obj.GetType();
            if (IsLeaf(t)) { sb.Append(pad).Append(label).Append(" = ").Append(Render(obj)).Append('\n'); return; }
            if (!seen.Add(obj)) { sb.Append(pad).Append(label).Append(" = <cycle: ").Append(t.Name).Append(">\n"); return; }

            sb.Append(pad).Append(label).Append(": ").Append(t.Name).Append('\n');

            if (obj is IEnumerable && depth > 0)
            {
                int i = 0;
                foreach (var item in (IEnumerable)obj)
                {
                    if (i >= MaxCollectionItems) { sb.Append(pad).Append("  … (truncated)\n"); break; }
                    Dump(sb, item, "[" + i + "]", indent + 1, depth - 1, seen);
                    i++;
                }
                if (i == 0) sb.Append(pad).Append("  (empty)\n");
                return;
            }

            if (depth <= 0) { sb.Append(pad).Append("  … (depth limit)\n"); return; }

            var fields = MemberResolver.FieldsInTokenOrder(t);
            for (int i = 0; i < fields.Length; i++)
            {
                if (Lines(sb) > MaxLines) { sb.Append(pad).Append("  … (line cap)\n"); return; }
                var f = fields[i];
                object v;
                try { v = f.GetValue(f.IsStatic ? null : obj); }
                catch (Exception ex) { sb.Append(pad).Append("  f[").Append(i).Append("] <threw: ").Append(ex.GetType().Name).Append(">\n"); continue; }
                string name = "f[" + i + "] " + (f.IsStatic ? "static " : "") + Pretty(f.FieldType);
                Dump(sb, v, name, indent + 1, depth - 1, seen);
            }
        }

        private static bool IsLeaf(Type t)
            => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
               || t == typeof(IntPtr) || t == typeof(UIntPtr)
               || (t.IsValueType && t.Assembly == typeof(int).Assembly); // BCL structs (DateTime, …)

        private static string Render(object v)
        {
            if (v is string) return "\"" + (string)v + "\"";
            return v.ToString();
        }

        private static string Pretty(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            var args = t.GetGenericArguments();
            var names = new string[args.Length];
            for (int i = 0; i < args.Length; i++) names[i] = Pretty(args[i]);
            int tick = t.Name.IndexOf('`');
            return (tick > 0 ? t.Name.Substring(0, tick) : t.Name) + "<" + string.Join(",", names) + ">";
        }

        private static int Lines(StringBuilder sb)
        {
            int n = 0;
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') n++;
            return n;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
