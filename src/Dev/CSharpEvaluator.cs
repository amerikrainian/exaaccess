#if DEBUG
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Mono.CSharp;

namespace ExaAccess.Dev
{
    /// <summary>
    /// Wraps Mono.CSharp's REPL so the dev driver can POST arbitrary C# and run it against the live game.
    /// State persists across calls (usings, variables defined in one eval are visible to the next), so the
    /// session behaves like a REPL. Ported from WrathAccess/TangledeepAccess. DEBUG-only.
    ///
    /// This is the single highest-leverage tool for an obfuscated game: it can enumerate the loaded types,
    /// reach the live <c>GameLogic</c> via <c>ExaAccess.GameState</c>, read the Sim model, and call methods
    /// by their de4dot names — turning static decompile-reading into live exploration.
    ///
    /// MUST be used from the game's main thread: evaluated code routinely touches engine/game objects. The
    /// dev server enqueues code and pumps it from the per-frame tick (GameLogic.method_25 prefix).
    /// </summary>
    internal sealed class CSharpEvaluator
    {
        private Evaluator _evaluator;
        private StringWriter _report; // compiler diagnostics land here

        private void Initialize()
        {
            _report = new StringWriter();
            var ctx = new CompilerContext(new CompilerSettings(), new StreamReportPrinter(_report));
            _evaluator = new Evaluator(ctx);

            // Make the game + mod assemblies visible to evaluated code. We must NOT re-reference the core
            // BCL assemblies the evaluator already imports (mscorlib/System/System.Core/...): adding them
            // again imports every type twice and makes even int/AppDomain/Select ambiguous (CS0433/CS0121).
            // The seed set doubles as the dedupe set, so duplicate GetAssemblies() entries are skipped too.
            var referenced = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mscorlib", "System", "System.Core", "System.Xml", "System.Xml.Linq",
                "System.Configuration", "System.Data", "Mono.CSharp", "Mono.Security",
                "netstandard",
            };
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (string.IsNullOrEmpty(name) || asm.IsDynamic || !referenced.Add(name)) continue;
                try { _evaluator.ReferenceAssembly(asm); }
                catch { /* not referenceable (reflection-only / load quirk); ignore */ }
            }

            try
            {
                _evaluator.Run(
                    "using System; using System.Linq; using System.Reflection; "
                    + "using System.Collections; using System.Collections.Generic; using ExaAccess;");
            }
            catch { /* a missing default namespace shouldn't sink the whole evaluator */ }
        }

        /// <summary>Compile and run <paramref name="code"/>; return output + result/errors.</summary>
        public string Eval(string code)
        {
            if (_evaluator == null) Initialize();

            var output = new StringWriter();
            TextWriter origOut = Console.Out;
            TextWriter origErr = Console.Error;
            int reportStart = _report.GetStringBuilder().Length;

            object value = null;
            bool hasValue = false;
            Exception thrown = null;

            Console.SetOut(output);
            Console.SetError(output);
            try
            {
                // Evaluate consumes one statement/expression at a time and returns the unconsumed
                // remainder; loop until it's all gone (or stalls / errors).
                string input = code;
                int guard = 0;
                while (!string.IsNullOrWhiteSpace(input) && guard++ < 10000)
                {
                    string remainder;
                    object res;
                    bool resSet;
                    try { remainder = _evaluator.Evaluate(input, out res, out resSet); }
                    catch (Exception e) { thrown = e; break; }
                    if (resSet) { value = res; hasValue = true; }
                    if (_report.GetStringBuilder().Length > reportStart) break; // a compile diagnostic was emitted
                    if (remainder == input) break; // no progress (incomplete input) - stop to avoid a spin
                    input = remainder;
                }
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }

            var sb = new StringBuilder();
            string captured = output.ToString();
            if (captured.Length > 0)
            {
                sb.Append(captured);
                if (!captured.EndsWith("\n")) sb.Append('\n');
            }
            string diagnostics = _report.ToString().Substring(reportStart).Trim();
            if (diagnostics.Length > 0) sb.Append("[compile] ").Append(diagnostics).Append('\n');
            if (thrown != null) sb.Append("[exception] ").Append(thrown).Append('\n');
            if (hasValue) sb.Append("=> ").Append(value == null ? "null" : value.ToString()).Append('\n');
            if (sb.Length == 0) sb.Append("(ok)\n");
            return sb.ToString();
        }
    }
}
#endif
