using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: InspectConstructors <assembly-path>");
    return 1;
}

var asmPath = args[0];
if (!File.Exists(asmPath))
{
    Console.Error.WriteLine($"File not found: {asmPath}");
    return 2;
}

using var asmDef = AssemblyDefinition.ReadAssembly(asmPath);
Console.WriteLine($"Loaded: {asmDef.Name.Name}");

var problematic = asmDef.MainModule.Types
    .Where(t => t.IsClass && !t.IsInterface)
    .Select(t => new { Type = t, PublicCtors = t.Methods.Where(m => m.IsConstructor && m.IsPublic) })
    .Where(x => x.PublicCtors.Count() > 1)
    .ToList();

if (!problematic.Any())
{
    Console.WriteLine("No types with >1 public constructor found.");
    return 0;
}

Console.WriteLine("Types with >1 public constructor:");
foreach (var p in problematic)
{
    Console.WriteLine($"{p.Type.FullName} - {p.PublicCtors.Count()} public ctors");
    foreach (var c in p.PublicCtors)
    {
        var ps = string.Join(", ", c.Parameters.Select(pi => pi.ParameterType.Name));
        Console.WriteLine($"  ctor({ps})");
    }
}

return 0;