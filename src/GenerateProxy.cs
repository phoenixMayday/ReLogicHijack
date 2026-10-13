using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class GenerateProxy
{
    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("Usage: GenerateProxy Terraria.exe ReLogicHijack.dll output-directory");
            }

            Directory.CreateDirectory(args[2]);

            using (var game = AssemblyDefinition.ReadAssembly(args[0]))
            {
                if (game.Name.Name != "Terraria") throw new InvalidOperationException("Expected the Terraria client.");

                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
                resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));

                // Cecil also needs Terraria's embedded dependencies when resolving types.
                resolver.ResolveFailure += delegate(object sender, AssemblyNameReference reference)
                {
                    var embedded = game.MainModule.Resources.OfType<EmbeddedResource>()
                        .SingleOrDefault(r => r.Name.EndsWith(
                            "." + reference.Name + ".dll", StringComparison.OrdinalIgnoreCase));

                    if (embedded == null) return null;

                    return AssemblyDefinition.ReadAssembly(new MemoryStream(embedded.GetResourceData()),
                        new ReaderParameters { AssemblyResolver = resolver });
                };

                var resource = game.MainModule.Resources.OfType<EmbeddedResource>()
                    .Single(r => r.Name == "Terraria.Libraries.ReLogic.ReLogic.dll");

                using (var stream = resource.GetResourceStream())
                using (var proxy = AssemblyDefinition.ReadAssembly(
                    stream, new ReaderParameters { AssemblyResolver = resolver }))
                using (var overlay = AssemblyDefinition.ReadAssembly(args[1]))
                {
                    if (proxy.Name.HasPublicKey)
                    {
                        throw new InvalidOperationException("This loader expects an unsigned ReLogic assembly.");
                    }

                    var module = proxy.MainModule;
                    var type = module.Types.Single(t => t.Name == "<Module>");
                    var initializer = type.Methods.SingleOrDefault(m => m.Name == ".cctor");

                    if (initializer == null)
                    {
                        initializer = new MethodDefinition(
                            ".cctor",
                            MethodAttributes.Private | MethodAttributes.Static |
                            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                            module.TypeSystem.Void);
                        type.Methods.Add(initializer);
                        initializer.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                    }

                    // Start the loader before any existing module-initializer code.
                    var start = overlay.MainModule.GetType("ReLogicHijack.Bootstrap")
                        .Methods.Single(m => m.Name == "Start");

                    initializer.Body.GetILProcessor().InsertBefore(
                        initializer.Body.Instructions[0],
                        Instruction.Create(OpCodes.Call, module.ImportReference(start)));

                    proxy.Write(Path.Combine(args[2], "ReLogic.dll"));
                    Console.WriteLine("Generated ReLogic proxy for " + game.Name.FullName);
                    Console.WriteLine("Original game EXE was read only.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
