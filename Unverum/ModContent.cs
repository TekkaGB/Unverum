#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unverum;

public class ModFileMeta
{
    private readonly Mod mod;

    private readonly List<InstallableModObject> objects;


    public ModFileMeta(Mod mod)
    {
        this.mod = mod;
        var modPath =
            $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{mod.name}";
        objects = Scan(modPath);
    }

    private static List<InstallableModObject> Scan(string path)
    {
        var files = new List<InstallableModObject>();
        files.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file).Equals(".pak", StringComparison.InvariantCultureIgnoreCase))
            .Select(pak => new Pak(path, pak)));

        // files.AddRange(Directory.GetDirectories(path, "*CoreMods", SearchOption.AllDirectories)
        //     .SelectMany(dirs => Directory.GetFiles(dirs, "*", SearchOption.AllDirectories))
        //     .Select(mod => new CoreMods(mod)));
        //
        // files.AddRange(Directory.GetDirectories(path, "*LogicMods", SearchOption.AllDirectories)
        //     .SelectMany(dirs => Directory.GetFiles(dirs, "*", SearchOption.AllDirectories))
        //     .Select(mod => new LogicMods(mod)));


        return files;
    }

    public Dictionary<string, string> GetFiles(InstallContext ctx)
    {
        var enabledPak = mod.paks.Where(pair => pair.Value)
            .Select(pair => pair.Key)
            // FIXME: update Mod pak names
            // .Select(path =>
            // {
            //     return Path.GetRelativePath(modRoot, path);
            // })
            .Select(Path.GetFullPath)
            .ToList();

        return objects
            .Where(o => o switch
            {
                Pak pak => enabledPak.Contains(pak.PakFullPath()),
                _ => true
            })
            .SelectMany(o => o.GetFiles(ctx))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}

public interface InstallableModObject
{
    public Dictionary<string, string> GetFiles(InstallContext ctx);
}

public class InstallContext
{
    public string? SigPath { get; }
    public string? SplashPath { get; }
    public string? MoviesPath { get; }
    public string? SoundsPath { get; }

    public InstallContext(string? sigPath, string? splashPath, string? moviesPath, string? soundsPath)
    {
        SigPath = sigPath;
        SplashPath = splashPath;
        MoviesPath = moviesPath;
        SoundsPath = soundsPath;
    }
}

class Pak : InstallableModObject
{
    private readonly string pakPath;
    private readonly string modRoot;
    private readonly List<string> files;

    private readonly bool hasSig;

    public Pak(string modRoot, string pakPath)
    {
        this.modRoot = modRoot;
        this.pakPath = Path.GetRelativePath(modRoot, pakPath);
        files = new List<string> { this.pakPath };

        var sig = Path.ChangeExtension(this.pakPath, ".sig");
        hasSig = File.Exists(Path.Join(this.modRoot, sig));
        if (hasSig) files.Add(sig);

        var utoc = Path.ChangeExtension(this.pakPath, ".utoc");
        var ucas = Path.ChangeExtension(this.pakPath, ".ucas");
        if (File.Exists(Path.Join(this.modRoot, utoc)) &&
            File.Exists(Path.Join(this.modRoot, ucas)))
        {
            files.AddRange(new[] { utoc, ucas });
        }
    }

    public Dictionary<string, string> GetFiles(InstallContext ctx)
    {
        var outputs = files.ToDictionary(s => Path.Join(modRoot, s), s => s);

        if (!hasSig && ctx.SigPath != null)
            outputs.Add(ctx.SigPath, Path.ChangeExtension(PakFullPath(), ".sig"));

        return outputs;
    }

    public string PakFullPath()
    {
        return Path.GetFullPath(Path.Join(modRoot, pakPath));
    }
}
