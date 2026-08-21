using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: NameMap <orig.exe> <deob.exe> <out.tsv>");
    return 2;
}

try
{
    var orig = Snapshot.Read(args[0]);
    var deob = Snapshot.Read(args[1]);
    var pairs = Align(deob, orig);
    int lines = Emit(pairs, args[2]);
    Console.WriteLine($"namemap: {pairs.Count} type pairs aligned, {lines} renames emitted -> {args[2]}");
    return 0;
}
catch (AlignmentException ex)
{
    Console.Error.WriteLine("NAMEMAP ALIGNMENT FAILED: " + ex.Message);
    Console.Error.WriteLine("(game updated? re-run de4dot and re-verify — see CLAUDE.md)");
    return 1;
}

static List<(TypeSnap Deob, TypeSnap Orig)> Align(Snapshot deob, Snapshot orig)
{
    var pairs = new List<(TypeSnap, TypeSnap)>();
    int j = 0, skipped = 0;
    int allowedSkips = orig.Types.Count - deob.Types.Count;
    if (allowedSkips < 0) throw new AlignmentException("deob has MORE types than orig — wrong inputs?");

    foreach (var d in deob.Types)
    {
        while (true)
        {
            if (j >= orig.Types.Count) throw new AlignmentException($"ran out of orig types pairing '{d.FullName}'.");
            var o = orig.Types[j];

            if (d.FullName == o.FullName)
            {
                pairs.Add((d, o));
                j++;
                break;
            }
            if (o.IsObfName && ShapeMatches(d, o))
            {
                pairs.Add((d, o));
                j++;
                break;
            }
            if (o.IsObfName)
            {
                // Shape-rejected obfuscated row: obfuscator junk de4dot stripped.
                Console.Error.WriteLine($"  skip orig '{Printable(o.FullName)}' (m{o.MethodCount}/f{o.FieldCount}) before deob '{d.FullName}' (m{d.MethodCount}/f{d.FieldCount})");
                j++;
                if (++skipped > allowedSkips)
                    throw new AlignmentException($"too many skips ({skipped} > {allowedSkips}) at deob '{d.FullName}' vs orig '{Printable(o.FullName)}'.");
                continue;
            }
            throw new AlignmentException($"real-name mismatch: deob '{d.FullName}' vs orig '{Printable(o.FullName)}' — order drifted.");
        }
    }
    skipped += orig.Types.Count - j; // trailing junk
    if (skipped != allowedSkips)
        throw new AlignmentException($"skip count {skipped} != expected {allowedSkips}.");

    // Anchor validation: every name-preserved pairing must be exact, and the classics must be there.
    int anchors = pairs.Count(p => p.Item1.FullName == p.Item2.FullName && !p.Item1.IsObfName);
    foreach (var must in new[] { "GameLogic", "DesktopScreen", "ControlPanelScreen", "LocString", "IScreen", "Texture" })
        if (!pairs.Any(p => p.Item1.FullName == must && p.Item2.FullName == must))
            throw new AlignmentException($"anchor type '{must}' did not pair name-equal.");
    Console.WriteLine($"namemap: {anchors} name-preserved anchor pairs validated.");
    return pairs;
}

// Tolerant: de4dot may strip obfuscator-injected MEMBERS from kept types, so orig may carry a few
// extra methods/fields. Never fewer; never a large drift; nesting must agree. Any greedy mistake
// this tolerance lets through is caught by the real-name anchor validation (hundreds of anchors).
static bool ShapeMatches(TypeSnap d, TypeSnap o)
    => d.IsNested == o.IsNested
       && o.MethodCount >= d.MethodCount && o.FieldCount >= d.FieldCount
       && (o.MethodCount - d.MethodCount) + (o.FieldCount - d.FieldCount) <= 8;

static string Printable(string s)
    => new string(s.Select(c => c > 126 || char.IsControl(c) ? '?' : c).ToArray());

// Pair members in row order; an orig row that can't pair (shape mismatch, or a constructor-name
// conflict) is a stripped obfuscator member and gets skipped, within the count-difference budget.
static List<(MemberSnap Deob, MemberSnap Orig)> AlignMembers(List<MemberSnap> deob, List<MemberSnap> orig)
{
    int budget = orig.Count - deob.Count;
    if (budget < 0) return null;
    var result = new List<(MemberSnap, MemberSnap)>(deob.Count);
    int j = 0;
    foreach (var dm in deob)
    {
        while (true)
        {
            if (j >= orig.Count) return null;
            var om = orig[j];
            bool ctorD = dm.Name is ".ctor" or ".cctor";
            bool ctorO = om.Name is ".ctor" or ".cctor";
            bool pairable = dm.Name == om.Name
                || (dm.IsStatic == om.IsStatic && dm.ParamCount == om.ParamCount && ctorD == ctorO);
            if (pairable)
            {
                result.Add((dm, om));
                j++;
                break;
            }
            if (--budget < 0) return null;
            j++;
        }
    }
    return result;
}

static int Emit(List<(TypeSnap Deob, TypeSnap Orig)> pairs, string outPath)
{
    var sb = new System.Text.StringBuilder();
    int lines = 0;
    var memberMismatches = new List<string>();

    foreach (var (d, o) in pairs)
    {
        if (d.Name != o.Name)
        {
            if (d.Namespace != o.Namespace)
                throw new AlignmentException($"namespace changed for '{d.FullName}' → '{o.FullName}' — unsupported.");
            sb.Append("T\t").Append(d.FullName).Append('\t').Append(o.Name).Append('\n');
            lines++;
        }

        // Members: two-pointer alignment in row order (orig may carry obfuscator-injected members
        // de4dot stripped). Failure marks the whole type unmapped and reported — referencing its
        // renamed members would then fail the module load, loudly, instead of misbinding.
        var methods = AlignMembers(d.Methods, o.Methods);
        var fields = AlignMembers(d.Fields, o.Fields);
        if (methods == null || fields == null)
        {
            memberMismatches.Add($"{d.FullName} (methods {d.Methods.Count}/{o.Methods.Count}, fields {d.Fields.Count}/{o.Fields.Count})");
            continue;
        }
        foreach (var (dm, om) in methods)
            if (dm.Name != om.Name) { sb.Append("M\t").Append(d.FullName).Append('\t').Append(dm.Name).Append('\t').Append(om.Name).Append('\n'); lines++; }
        foreach (var (df, of) in fields)
            if (df.Name != of.Name) { sb.Append("F\t").Append(d.FullName).Append('\t').Append(df.Name).Append('\t').Append(of.Name).Append('\n'); lines++; }
    }

    if (memberMismatches.Count > 0)
    {
        Console.WriteLine("namemap: " + memberMismatches.Count + " type(s) with member-count drift left unmapped:");
        foreach (var m in memberMismatches) Console.WriteLine("  " + m);
    }
    File.WriteAllText(outPath, sb.ToString());
    return lines;
}

sealed class AlignmentException : Exception
{
    public AlignmentException(string message) : base(message) { }
}

sealed record MemberSnap(string Name, bool IsStatic, int ParamCount);

sealed class TypeSnap
{
    public string Namespace = "";
    public string Name = "";
    public string FullName = ""; // Cecil format: "Ns.Name" / "Parent/Nested"
    public bool IsNested;
    public bool IsObfName;
    public int MethodCount;
    public int FieldCount;
    public List<MemberSnap> Methods = new();
    public List<MemberSnap> Fields = new();
}

sealed class Snapshot
{
    public List<TypeSnap> Types = new();

    public static Snapshot Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();

        // First pass: names + nesting (FullName needs the declaring chain).
        var byHandle = new Dictionary<TypeDefinitionHandle, TypeSnap>();
        var order = new List<(TypeDefinitionHandle Handle, TypeSnap Snap)>();
        foreach (var h in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(h);
            var snap = new TypeSnap
            {
                Namespace = md.GetString(td.Namespace),
                Name = md.GetString(td.Name),
                IsNested = td.IsNested,
            };
            // Eazfuscator names: base64-ish (#=q…, '='), unicode-junk (zero-width chars), whitespace.
            snap.IsObfName = snap.Name.Any(c => c == '=' || c == '#' || char.IsWhiteSpace(c) || c > 127);
            byHandle[h] = snap;
            order.Add((h, snap));
        }
        foreach (var (h, snap) in order)
        {
            var td = md.GetTypeDefinition(h);
            if (td.IsNested)
                snap.FullName = FullNameOf(md, td.GetDeclaringType(), byHandle) + "/" + snap.Name;
            else
                snap.FullName = snap.Namespace.Length == 0 ? snap.Name : snap.Namespace + "." + snap.Name;
        }

        // Second pass: members in row order.
        foreach (var (h, snap) in order)
        {
            var td = md.GetTypeDefinition(h);
            foreach (var mh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                var sig = m.DecodeSignature(new ShapeSigProvider(), null);
                snap.Methods.Add(new MemberSnap(
                    md.GetString(m.Name),
                    (m.Attributes & MethodAttributes.Static) != 0,
                    sig.ParameterTypes.Length));
            }
            foreach (var fh in td.GetFields())
            {
                var f = md.GetFieldDefinition(fh);
                snap.Fields.Add(new MemberSnap(
                    md.GetString(f.Name),
                    (f.Attributes & FieldAttributes.Static) != 0,
                    0));
            }
            snap.MethodCount = snap.Methods.Count;
            snap.FieldCount = snap.Fields.Count;
        }

        var result = new Snapshot();
        result.Types = order.Select(o => o.Snap).ToList();
        return result;
    }

    private static string FullNameOf(MetadataReader md, TypeDefinitionHandle h, Dictionary<TypeDefinitionHandle, TypeSnap> byHandle)
    {
        var td = md.GetTypeDefinition(h);
        var snap = byHandle[h];
        if (!td.IsNested)
            return snap.Namespace.Length == 0 ? snap.Name : snap.Namespace + "." + snap.Name;
        return FullNameOf(md, td.GetDeclaringType(), byHandle) + "/" + snap.Name;
    }
}

/// <summary>Signature decoder that only cares about arity — every type decodes to a placeholder.</summary>
sealed class ShapeSigProvider : ISignatureTypeProvider<string, object>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "t";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => "t";
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => "t";
    public string GetSZArrayType(string elementType) => "t";
    public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) => "t";
    public string GetArrayType(string elementType, ArrayShape shape) => "t";
    public string GetByReferenceType(string elementType) => "t";
    public string GetPointerType(string elementType) => "t";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "t";
    public string GetGenericMethodParameter(object genericContext, int index) => "t";
    public string GetGenericTypeParameter(object genericContext, int index) => "t";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => "t";
    public string GetPinnedType(string elementType) => "t";
    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => "t";
}
