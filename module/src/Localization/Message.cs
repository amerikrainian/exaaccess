using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ExaAccess.Localization
{
    /// <summary>
    /// A lazily-resolved piece of speakable text, ported from WrathAccess: either raw text or a
    /// localization (table, key) reference, optionally with named {var} substitutions whose values
    /// are themselves Messages (resolved at speak time, so composed announcements always read the
    /// live localization). Composition: <c>a + b</c> joins with a space; <see cref="Join"/> with any
    /// separator. WrathAccess's TMP rich-text stripping is dropped â€” EXAPUNKS strings carry none;
    /// whitespace hygiene lives in Tts.Clean.
    /// </summary>
    public sealed class Message
    {
        /// <summary>(table, key) â†’ localized string or null. Installed by LocalizationManager.
        /// Null resolver (or a miss) falls back to the key text, so nothing ever goes silent.</summary>
        public static Func<string, string, string> LocalizationResolver;

        private static readonly Regex VariablePattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        public static readonly Message Empty = Raw("");

        private readonly string _rawText;
        private readonly string _table;
        private readonly string _key;
        private readonly Dictionary<string, Message> _vars;
        private readonly List<Message> _parts;    // composite (Join / operator+)
        private readonly string _separator;

        private Message(string rawText, string table, string key, Dictionary<string, Message> vars)
        {
            _rawText = rawText;
            _table = table;
            _key = key;
            _vars = vars;
        }

        private Message(List<Message> parts, string separator)
        {
            _parts = parts;
            _separator = separator;
        }

        public static Message Raw(string text) => new Message(text ?? "", null, null, null);
        public static Message Raw(string text, object vars) => new Message(text ?? "", null, null, ObjectToDict(vars));

        /// <summary>Null in, null out â€” for optional labels.</summary>
        public static Message MaybeRaw(string text) => text == null ? null : Raw(text);

        public static Message Localized(string table, string key) => new Message(null, table, key, null);
        public static Message Localized(string table, string key, object vars) => new Message(null, table, key, ObjectToDict(vars));

        public static Message Join(string separator, params Message[] parts)
        {
            var list = new List<Message>();
            foreach (var p in parts)
                if (p != null && !p.IsEmpty) list.Add(p);
            return new Message(list, separator ?? " ");
        }

        /// <summary>Space-joined concatenation; composites flatten so chains stay one list.</summary>
        public static Message operator +(Message left, Message right)
        {
            var list = new List<Message>();
            Flatten(left, list);
            Flatten(right, list);
            return new Message(list, " ");
        }

        private static void Flatten(Message m, List<Message> into)
        {
            if (m == null || m.IsEmpty) return;
            if (m._parts != null && m._separator == " ") { into.AddRange(m._parts); return; }
            into.Add(m);
        }

        public bool IsEmpty
        {
            get
            {
                if (_parts != null) return _parts.Count == 0;
                return _rawText != null && _rawText.Length == 0 && _vars == null;
            }
        }

        public string Resolve()
        {
            if (_parts != null) return ResolveComposite();
            string text = _rawText != null
                ? _rawText
                : (LocalizationResolver != null ? LocalizationResolver(_table, _key) : null) ?? _key ?? "";
            if (_vars != null && _vars.Count > 0)
                text = SubstituteVars(text, _vars);
            return text;
        }

        public override string ToString() => Resolve();

        private string ResolveComposite()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _parts.Count; i++)
            {
                string piece = _parts[i].Resolve();
                if (string.IsNullOrEmpty(piece)) continue;
                if (sb.Length > 0) sb.Append(_separator);
                sb.Append(piece);
            }
            return sb.ToString();
        }

        private static string SubstituteVars(string text, Dictionary<string, Message> vars)
        {
            // An unmatched {token} stays literal â€” a missing arg reads oddly but never crashes or blanks.
            return VariablePattern.Replace(text, m =>
                vars.TryGetValue(m.Groups[1].Value, out var v) ? v.Resolve() : m.Value);
        }

        private static Dictionary<string, Message> ObjectToDict(object vars)
        {
            if (vars == null) return null;
            var dict = new Dictionary<string, Message>();
            foreach (PropertyInfo p in vars.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = p.GetValue(vars, null);
                dict[p.Name] = value as Message ?? Raw(value?.ToString() ?? "");
            }
            return dict;
        }
    }
}

