using System;
using System.IO;
using System.Reflection;

namespace Echopunks.Modularity
{
    /// <summary>
    /// Loads (and hot-reloads) Echopunks.Module.dll. net48 port of NonVisualCalculus's ModuleLoader:
    /// no AssemblyLoadContext here, so the module is Assembly.Load(byte[])'d into the AppDomain — the
    /// on-disk file stays unlocked (a build can overwrite it with the game running) and each load gets
    /// fresh statics. Old copies are pinned until process exit; that leak is a dev-loop cost only and
    /// mirrors NVC's own collectible-context-that-never-collects reality.
    ///
    /// LOAD-THEN-SWAP: the candidate module is fully constructed and Load()ed before the previous one
    /// is disposed or the public reference moves — a broken build leaves the running module untouched.
    /// Consequence (same as NVC): the new module's Harmony patches exist before the old module
    /// unpatches, hence the per-load-unique-id rule on <see cref="IModModule"/>.
    /// </summary>
    internal sealed class ModuleLoader
    {
        private readonly string _modulePath;
        private readonly ModHost _host;

        public IModModule Module { get; private set; }
        public int Generation { get; private set; }

        public ModuleLoader(string modulePath, ModHost host)
        {
            _modulePath = modulePath;
            _host = host;
        }

        /// <summary>Load the current on-disk module and swap it in. Main thread only (module Load and
        /// Dispose touch Harmony and game state). Returns false — with the old module still running —
        /// on any failure.</summary>
        public bool Reload()
        {
            IModModule candidate = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(_modulePath);
                // The module is compiled against the deob game names; rewrite its game references to
                // the shipping names before loading (see GameRefRemapper).
                string mapPath = Path.Combine(Path.GetDirectoryName(_modulePath), "Echopunks", "namemap.tsv");
                bytes = GameRefRemapper.Remap(bytes, mapPath);
                var asm = Assembly.Load(bytes);

                Type impl = null;
                foreach (var t in asm.GetTypes())
                {
                    if (typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    { impl = t; break; }
                }
                if (impl == null)
                {
                    Log.Error("[module] no IModModule implementation in " + Path.GetFileName(_modulePath) + ".");
                    return false;
                }

                candidate = (IModModule)Activator.CreateInstance(impl);
                _host.ModuleGeneration = Generation + 1; // the module sees its own generation during Load
                candidate.Load(_host);
            }
            catch (Exception ex)
            {
                Log.Error("[module] load failed — keeping the running module (if any)", ex);
                try { candidate?.Dispose(); } catch { }
                _host.ModuleGeneration = Generation;
                return false;
            }

            var old = Module;
            Module = candidate;
            Generation++;
            _host.ModuleGeneration = Generation;
            Log.Info("[module] " + (old == null ? "loaded" : "reloaded") + " (generation " + Generation + ", "
                + File.GetLastWriteTime(_modulePath).ToString("HH:mm:ss") + " dll).");

            if (old != null)
            {
                try { old.Dispose(); }
                catch (Exception ex) { Log.Error("[module] old module Dispose failed (continuing on the new one)", ex); }
            }
            return true;
        }
    }
}
