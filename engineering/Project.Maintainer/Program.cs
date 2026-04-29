using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;

namespace Project.Maintainer;

class Program
{
    public const string Upstream = "https://github.com/moegodot/NewProject-CS";
    public const string RootMarker = ".godot.async.proj.root";

    public static string RepoDirectory
    {
        get
        {
            field ??= GetRoot();
            return field;

            static string GetRoot()
            {
                var current = Directory.GetCurrentDirectory();
                var file = Path.Combine(current, RootMarker);
                while (!File.Exists(file))
                {
                    current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException($"failed to find {RootMarker}");
                    file = Path.Combine(current, RootMarker);
                }

                return current;
            }
        }
    } = null!;

    public static string UpdateContentDirectoryName
    {
        get
        {
            field ??= GetRoot();
            return field;

            static string GetRoot()
            {
                var name = $"./{Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(RepoDirectory)))}-kwi-godot/";
                return name;
            }
        }
    }

    public static string UpdateContentDirectory
    {
        get
        {
            return Path.Combine(Path.GetTempPath(), UpdateContentDirectoryName);
        }
    }

    static void Main(string[] args)
    {
        if (!Directory.Exists(UpdateContentDirectory))
        {
            Helper.ShallowClone(Upstream, Path.GetTempPath(),UpdateContentDirectoryName);
        }

        Helper.Update(UpdateContentDirectory);

        // new Maintainer(),
        Maintenance[] maintenances = [new ProjectProps(),new ProjectTargets(),new Icon(),new Maintainer()];

        foreach (Maintenance maintenance in maintenances)
        {
            OutColor($"execute maintainer {maintenance.GetType().Name}");
            maintenance.Maintain();
        }
    }
}
