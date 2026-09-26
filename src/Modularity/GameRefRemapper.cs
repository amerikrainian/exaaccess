using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Echopunks.Modularity
{
    /// <summary>
    /// The load-time half of the typed-game-access pipeline. The module COMPILES against
    /// game/EXAPUNKS-deob.exe (de4dot's readable names — never shipped), and this pass rewrites the
    /// module's references to the game assembly ("Burbank" in both binaries, so assembly identity
    /// needs no touch) from deob names to the shipping obfuscated names, using the namemap.tsv the
    /// build generates (tools/NameMap: row-order alignment of orig vs deob, anchor-validated).
    /// Runs on the module BYTES before Assembly.Load — the same seam hot reload already uses.
    ///
    /// Only names are rewritten (member/type refs; string literals are untouched — reflection by
    /// name against the game must go through the module's Deobf helper instead). Any failure throws:
    /// a module bound to deob names would die lazily at JIT with confusing per-frame errors, so the
    /// load must fail loudly up front instead.
    /// </summary>
    internal static class GameRefRemapper
    {
        private const string GameAssemblyName = "Burbank";

        public static byte[] Remap(byte[] moduleBytes, string mapPath)
        {
            if (!File.Exists(mapPath))
                throw new FileNotFoundException("namemap.tsv missing — the module cannot bind to the game without it.", mapPath);

            var typeMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var methodMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var fieldMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(mapPath))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                switch (parts[0])
                {
                    case "T": typeMap[parts[1]] = parts[2]; break;
                    case "M": methodMap[parts[1] + "\n" + parts[2]] = parts[3]; break;
                    case "F": fieldMap[parts[1] + "\n" + parts[2]] = parts[3]; break;
                }
            }

            using (var input = new MemoryStream(moduleBytes))
            using (var module = ModuleDefinition.ReadModule(input))
            {
                int members = 0, types = 0;

                // Members first (their lookup keys use the DEOB declaring-type name, which the type
                // pass below rewrites) — and via the METHOD BODIES, not GetMemberReferences():
                // Cecil materializes a fresh reference per call site for members on generic-instance
                // parents (GClass52<bool>::method_0), so renaming the table view never reaches those
                // IL operands. Walking operands covers every ref (including ldtoken); renames are
                // idempotent when an object is shared across sites.
                foreach (var type in module.GetTypes())
                {
                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;
                        foreach (var ins in method.Body.Instructions)
                        {
                            switch (ins.Operand)
                            {
                                case GenericInstanceMethod gim:
                                    if (RenameMember(gim.ElementMethod, methodMap)) members++;
                                    break;
                                case MethodReference m when !(m is MethodDefinition):
                                    if (RenameMember(m, methodMap)) members++;
                                    break;
                                case FieldReference f when !(f is FieldDefinition):
                                    if (RenameMember(f, fieldMap)) members++;
                                    break;
                            }
                        }
                    }
                }

                // Types: snapshot keys before renaming — a nested ref's FullName includes its
                // (possibly renamed) parents.
                var typeRefs = module.GetTypeReferences()
                    .Where(IsGameRef)
                    .Select(tr => new { Ref = tr, Key = tr.FullName })
                    .ToList();
                foreach (var t in typeRefs)
                {
                    if (typeMap.TryGetValue(t.Key, out var obf))
                    {
                        t.Ref.Name = obf;
                        types++;
                    }
                }

                Log.Info("[remap] module remapped to shipping names: " + types + " type ref(s), " + members + " member ref(s).");
                var output = new MemoryStream();
                module.Write(output);
                return output.ToArray();
            }
        }

        private static bool RenameMember(MemberReference mr, Dictionary<string, string> map)
        {
            var declaring = mr.DeclaringType;
            var element = declaring is GenericInstanceType git ? git.ElementType : declaring;
            if (!IsGameRef(element)) return false;
            if (!map.TryGetValue(element.FullName + "\n" + mr.Name, out var obf)) return false;
            mr.Name = obf;
            return true;
        }

        // A reference into the game assembly — resolve nested refs to their outermost declarer,
        // whose scope names the assembly.
        private static bool IsGameRef(TypeReference tr)
        {
            if (tr == null) return false;
            while (tr.DeclaringType != null) tr = tr.DeclaringType;
            return tr.Scope != null && tr.Scope.Name == GameAssemblyName;
        }
    }
}
