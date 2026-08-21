using System;

namespace ExaAccess.Speech
{
    /// <summary>
    /// The primary handler: Prism's best-available backend (NVDA/JAWS directly, else SAPI/OneCore via
    /// Prism). This is the engine logic that used to live inline in <see cref="Tts"/>, reshaped into
    /// the WrathAccess handler contract. prism.dll sits next to the game exe (the process working dir).
    /// </summary>
    internal sealed class PrismHandler : ISpeechHandler
    {
        private IntPtr _ctx = IntPtr.Zero;
        private IntPtr _backend = IntPtr.Zero;
        private PrismNative.BackendFeatures _features;

        public string Key => "prism";

        public bool Detect()
        {
            // The real probe is loading; the dll may be present but backendless (no screen reader, no
            // SAPI — rare). Cheap existence check first so a missing dll doesn't throw per boot.
            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, "prism.dll"))
                    || System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prism.dll"));
            }
            catch { return true; } // fall through to Load, which handles failure properly
        }

        public bool Load()
        {
            try
            {
                _ctx = PrismNative.Init(IntPtr.Zero);
                if (_ctx == IntPtr.Zero) { Log.Error("[speech] prism_init returned null (dll loaded but init failed)."); return false; }

                _backend = PrismNative.RegistryCreateBest(_ctx);
                if (_backend == IntPtr.Zero) { Log.Error("[speech] no usable Prism backend on this machine."); Unload(); return false; }

                var err = PrismNative.BackendInitialize(_backend);
                if (err != PrismNative.PrismError.Ok && err != PrismNative.PrismError.AlreadyInitialized)
                {
                    Log.Error("[speech] Prism backend initialize failed (" + err + ").");
                    Unload();
                    return false;
                }

                _features = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(_backend);
                Log.Info("[speech] Prism backend: " + (PrismNative.BackendName(_backend) ?? "<unknown>")
                    + " (features=0x" + ((ulong)_features).ToString("X") + ")");
                return true;
            }
            catch (DllNotFoundException)
            {
                Log.Info("[speech] prism.dll not found (or a dependency missing, e.g. the VC++ runtime) — trying the next handler.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("[speech] Prism load failed", ex);
                Unload();
                return false;
            }
        }

        public void Unload()
        {
            if (_backend != IntPtr.Zero)
            {
                try { PrismNative.BackendStop(_backend); } catch { }
                try { PrismNative.BackendFree(_backend); } catch { }
                _backend = IntPtr.Zero;
            }
            if (_ctx != IntPtr.Zero)
            {
                try { PrismNative.Shutdown(_ctx); } catch { }
                _ctx = IntPtr.Zero;
            }
            _features = 0;
        }

        public bool Speak(string text, bool interrupt)
        {
            if (_backend == IntPtr.Zero) return false;
            return PrismNative.BackendSpeak(_backend, text, interrupt) == PrismNative.PrismError.Ok;
        }

        public bool Output(string text, bool interrupt)
        {
            if (_backend == IntPtr.Zero) return false;
            // prism_backend_output drives speech + braille when supported; else fall back to speak.
            if ((_features & PrismNative.BackendFeatures.SupportsOutput) != 0)
            {
                var err = PrismNative.BackendOutput(_backend, text, interrupt);
                if (err == PrismNative.PrismError.Ok) return true;
                if (err != PrismNative.PrismError.NotImplemented)
                    Log.Info("[speech] Prism output -> " + err + ", falling back to speak.");
            }
            return Speak(text, interrupt);
        }

        public void Silence()
        {
            if (_backend == IntPtr.Zero) return;
            try { PrismNative.BackendStop(_backend); } catch { }
        }
    }
}
