using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("usage: TypeMap <assembly path>   (prints index<TAB>full type name, TypeDef row order)");
    return 2;
}

using var fs = File.OpenRead(args[0]);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

int index = 0;
foreach (var handle in md.TypeDefinitions)
{
    var td = md.GetTypeDefinition(handle);
    string ns = md.GetString(td.Namespace);
    string name = md.GetString(td.Name);
    Console.WriteLine(index + "\t" + (ns.Length == 0 ? name : ns + "." + name));
    index++;
}
return 0;
