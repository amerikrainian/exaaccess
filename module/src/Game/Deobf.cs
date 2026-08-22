using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace ExaAccess.Game
{
    /// <summary>
    /// The last resort of the typed-access pipeline: the compiler + load-time remap cover every
    /// PUBLIC game member, but C# cannot reference private members, and string-based reflection
    /// doesn't remap. This helper reads the same namemap.tsv the remapper uses and resolves a
    /// private member from its deob name. Works on renamed types too: the map's T rows translate
    /// the runtime (shipping) type name back to the deob name the M/F rows are keyed by.
    /// </summary>
    internal static class Deobf
    {
        private static Dictionary<string, string> _methods;
        private static Dictionary<string, string> _fields;
        private static Dictionary<string, string> _typeToDeob; // shipping full name -> deob name

        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static MethodInfo Method(Type type, string deobName)
        {
            Load();
            string name = _methods != null && _methods.TryGetValue(DeobTypeName(type) + "\n" + deobName, out var obf) ? obf : deobName;
            var m = type.GetMethod(name, All);
            if (m == null) Log.Error("[deobf] method " + DeobTypeName(type) + "." + deobName + " (-> '" + name + "') not found.");
            return m;
        }

        public static FieldInfo Field(Type type, string deobName)
        {
            Load();
            string name = _fields != null && _fields.TryGetValue(DeobTypeName(type) + "\n" + deobName, out var obf) ? obf : deobName;
            var f = type.GetField(name, All);
            if (f == null) Log.Error("[deobf] field " + DeobTypeName(type) + "." + deobName + " (-> '" + name + "') not found.");
            return f;
        }

        // The M/F rows are keyed by DEOB type name; at runtime the remapped typeof() gives the
        // SHIPPING name — the T rows bridge back. Name-preserved types pass through unchanged.
        private static string DeobTypeName(Type type)
        {
            string n = type.FullName;
            return _typeToDeob != null && _typeToDeob.TryGetValue(n, out var deob) ? deob : n;
        }

        private static void Load()
        {
            if (_methods != null) return;
            _methods = new Dictionary<string, string>(StringComparer.Ordinal);
            _fields = new Dictionary<string, string>(StringComparer.Ordinal);
            _typeToDeob = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                // Next to the HOST dll (this assembly is byte-loaded and has no location).
                string path = Path.Combine(
                    Path.GetDirectoryName(typeof(Log).Assembly.Location), "ExaAccess", "namemap.tsv");
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 3 && parts[0] == "T") _typeToDeob[parts[2]] = parts[1];
                    else if (parts.Length < 4) continue;
                    else if (parts[0] == "M") _methods[parts[1] + "\n" + parts[2]] = parts[3];
                    else if (parts[0] == "F") _fields[parts[1] + "\n" + parts[2]] = parts[3];
                }
            }
            catch (Exception ex) { Log.Error("[deobf] namemap load failed", ex); }
        }
    }

    /// <summary>Capture a MethodInfo from a compiled call expression — the ldtoken inside gets
    /// remapped like any other member reference, so this is the safe way to name a PUBLIC game
    /// method for Harmony patching (string lookups would not remap).</summary>
    internal static class Expr
    {
        public static MethodInfo MethodOf(Expression<Action> call)
            => ((MethodCallExpression)call.Body).Method;
    }
}
