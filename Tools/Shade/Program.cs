using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;

// Gives a VSIX its own copy of the System.Text.Json stack under private assembly names.
//
// In the 32-bit TcXaeShell there are no binding redirects for System.Text.Json and its dependencies,
// so each of those identities is resolved once per process by whoever asks first, through the
// AssemblyResolve chain. Beckhoff's TcTestClient (TF1140) answers there with whatever it finds by
// simple name, e.g. Microsoft.Bcl.AsyncInterfaces 10 for a request for 6. Assemblies under private
// names are never requested by anyone else, so that cannot happen to them.
//
//   Shade --dir <folder> --stack <folder with the stack DLLs> --prefix <Prefix.> --key <snk>
//         [--own <AssemblyName>=<snk>]... [--names a,b,c] [--search <dir>]... [--report <file>]
//
// 1. Every stack DLL named in --names is written to <dir>\<Prefix><Name>.dll under that assembly
//    name, with its references to the other stack DLLs renamed as well, signed with --key.
// 2. Every DLL in <dir> that references a stack DLL is rewritten to the private name and the stack
//    version (never older than the original reference) and re-signed: with its own key when --own
//    names one, otherwise with --key, in which case references to it get the new token as well.
// 3. --report lists the file names written, one per line.
static class Program
{
    static readonly string[] DefaultNames =
    {
        "System.Text.Json", "System.Text.Encodings.Web", "Microsoft.Bcl.AsyncInterfaces", "System.IO.Pipelines",
    };

    // Loaded by the shell from its own folders; never rewritten.
    static readonly string[] Excluded =
    {
        "Microsoft.VisualStudio.", "EnvDTE", "stdole", "Microsoft.Build", "Microsoft.ServiceHub",
        "StreamJsonRpc", "Nerdbank.", "MessagePack", "Community.VisualStudio.Toolkit",
    };

    static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Shade: error: " + ex);
            return 1;
        }
    }

    static int Run(string[] args)
    {
        string dir = null, stack = null, prefix = null, key = null, report = null;
        var own = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = DefaultNames.ToList();
        var search = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework\v4.0.30319"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework\v4.0.30319\WPF"),
        };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dir": dir = args[++i]; break;
                case "--stack": stack = args[++i]; break;
                case "--prefix": prefix = args[++i]; break;
                case "--key": key = args[++i]; break;
                case "--report": report = args[++i]; break;
                case "--search": search.Add(args[++i]); break;
                case "--names": names = args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); break;
                case "--own":
                    var kv = args[++i].Split(new[] { '=' }, 2);
                    own[kv[0]] = kv[1];
                    break;
                default:
                    Console.Error.WriteLine("Shade: error: unknown argument " + args[i]);
                    return 2;
            }
        }

        if (dir == null || stack == null || prefix == null || key == null)
        {
            Console.Error.WriteLine("Shade: error: usage: --dir <folder> --stack <folder> --prefix <Prefix.> --key <snk> [--own Name=snk] [--names a,b] [--search dir] [--report file]");
            return 2;
        }

        var resolver = new DefaultAssemblyResolver();
        foreach (var d in new[] { dir, stack }.Concat(search).Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(d))
                resolver.AddSearchDirectory(d);
        var reader = new ReaderParameters { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver };

        var keyBlob = File.ReadAllBytes(key);
        var keyToken = TokenOf(keyBlob);
        var written = new List<string>();

        // Original stack name -> private name and the version shipped in --stack
        var stackMap = new Dictionary<string, KeyValuePair<string, Version>>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names)
        {
            var path = Path.Combine(stack, n + ".dll");
            if (!File.Exists(path))
                throw new FileNotFoundException("stack assembly not found", path);
            stackMap[n] = new KeyValuePair<string, Version>(prefix + n, System.Reflection.AssemblyName.GetAssemblyName(path).Version);
        }

        // 1. Private copies of the stack
        foreach (var entry in stackMap)
        {
            var priv = entry.Value.Key;
            using (var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(Path.Combine(stack, entry.Key + ".dll"))), reader))
            {
                asm.Name.Name = priv;
                asm.MainModule.Name = priv + ".dll";
                asm.Name.PublicKey = PublicKeyOf(keyBlob);
                RewriteStackRefs(asm.MainModule, stackMap, keyToken);
                asm.Write(Path.Combine(dir, priv + ".dll"), new WriterParameters { StrongNameKeyBlob = keyBlob });
            }
            written.Add(priv + ".dll");
            Console.WriteLine("Shade: private {0} {1}", priv, entry.Value.Value);
        }

        // 2. Rewrite the folder's assemblies
        var modules = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || stackMap.ContainsKey(n))
                continue;
            if (Excluded.Any(e => n.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                continue;
            try
            {
                modules[f] = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(f)), reader);
            }
            catch (BadImageFormatException)
            {
                // native DLL
            }
        }

        var dirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in modules)
            if (RewriteStackRefs(m.Value.MainModule, stackMap, keyToken))
                dirty.Add(m.Key);

        // A rewritten strong-named assembly without its own key is re-signed with --key, which changes
        // its token, so every reference to it changes and those assemblies are rewritten too.
        var retoken = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var f in dirty.ToList())
            {
                var asm = modules[f];
                if (asm.Name.HasPublicKey && !own.ContainsKey(asm.Name.Name) && !retoken.ContainsKey(asm.Name.Name))
                {
                    retoken[asm.Name.Name] = keyToken;
                    changed = true;
                }
            }
            foreach (var m in modules)
            {
                foreach (var r in m.Value.MainModule.AssemblyReferences)
                {
                    if (retoken.TryGetValue(r.Name, out var tok) && !r.PublicKeyToken.SequenceEqual(tok))
                    {
                        r.PublicKeyToken = tok;
                        if (dirty.Add(m.Key))
                            changed = true;
                    }
                }
            }
        }

        foreach (var f in dirty)
        {
            var asm = modules[f];
            var n = asm.Name.Name;
            byte[] blob = null;
            if (asm.Name.HasPublicKey)
            {
                blob = own.TryGetValue(n, out var ownKey) ? File.ReadAllBytes(ownKey) : keyBlob;
                asm.Name.PublicKey = PublicKeyOf(blob);
            }
            asm.Write(f, blob != null ? new WriterParameters { StrongNameKeyBlob = blob } : new WriterParameters());
            written.Add(Path.GetFileName(f));
            Console.WriteLine("Shade: rewrote {0}{1}", n, blob == null ? "" : own.ContainsKey(n) ? " (own key)" : " (re-signed)");
        }

        foreach (var asm in modules.Values)
            asm.Dispose();

        if (report != null)
            File.WriteAllLines(report, written);
        return 0;
    }

    static bool RewriteStackRefs(ModuleDefinition module, Dictionary<string, KeyValuePair<string, Version>> map, byte[] token)
    {
        bool any = false;
        foreach (var r in module.AssemblyReferences)
        {
            if (!map.TryGetValue(r.Name, out var p))
                continue;
            if (r.Version > p.Value)
                throw new InvalidOperationException($"{module.Assembly.Name.Name} references {r.Name} {r.Version}, newer than the stack {p.Value}");
            r.Name = p.Key;
            r.Version = p.Value;
            r.PublicKeyToken = token;
            any = true;
        }
        return any;
    }

    // snk (PRIVATEKEYBLOB) -> strong-name public key blob
    static byte[] PublicKeyOf(byte[] snk)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.ImportCspBlob(snk);
            var pub = rsa.ExportCspBlob(false);
            var result = new byte[12 + pub.Length];
            BitConverter.GetBytes(0x00002400).CopyTo(result, 0); // CALG_RSA_SIGN
            BitConverter.GetBytes(0x00008004).CopyTo(result, 4); // CALG_SHA1
            BitConverter.GetBytes(pub.Length).CopyTo(result, 8);
            pub.CopyTo(result, 12);
            return result;
        }
    }

    static byte[] TokenOf(byte[] snk)
    {
        using (var sha = SHA1.Create())
        {
            var hash = sha.ComputeHash(PublicKeyOf(snk));
            return hash.Skip(hash.Length - 8).Reverse().ToArray();
        }
    }
}
