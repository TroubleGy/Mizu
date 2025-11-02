using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Mizu;

public static class JarProcessor
{
    public static List<TreeViewItem> ExtractStructure(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var root = new TreeViewItem { Header = Path.GetFileName(jarPath), Tag = null, IsExpanded = true };

        foreach (var entry in archive.Entries.OrderBy(e => e.FullName))
        {
            var parts = entry.FullName.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            TreeViewItem parent = root;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLast = i == parts.Length - 1;
                TreeViewItem? existing = parent.Items.OfType<TreeViewItem>().FirstOrDefault(x => (string)x.Header == part);
                if (existing == null)
                {
                    var node = new TreeViewItem { Header = part };
                    if (isLast && !entry.FullName.EndsWith("/"))
                        node.Tag = entry.FullName;
                    parent.Items.Add(node);
                    parent = node;
                }
                else parent = existing;
            }
        }
        return new List<TreeViewItem> { root };
    }

    public static bool IsImage(string path)
    {
        string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
        return exts.Any(e => path.EndsWith(e, System.StringComparison.OrdinalIgnoreCase));
    }

    public static BitmapImage? ExtractImage(string jarPath, string entryPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);
            var entry = archive.GetEntry(entryPath);
            if (entry == null) return null;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    public static List<TreeViewItem>? ExtractStructureFromInnerJar(string mainJar, string innerJarEntry)
    {
        try
        {
            using var outer = ZipFile.OpenRead(mainJar);
            var entry = outer.GetEntry(innerJarEntry);
            if (entry == null) return null;
            string temp = Path.Combine(Path.GetTempPath(), "Mizu", Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            string innerPath = Path.Combine(temp, Path.GetFileName(innerJarEntry));
            using var s = entry.Open();
            using var fs = File.Create(innerPath);
            s.CopyTo(fs);
            fs.Close();
            return ExtractStructure(innerPath);
        }
        catch
        {
            return null;
        }
    }

    public static string ReadTextFromJar(string jarPath, string entryPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);
            var entry = archive.GetEntry(entryPath);
            if (entry == null) return "// File not found";
            using var stream = entry.Open();
            if (IsBinary(entry.FullName))
                return $"[{entry.FullName}] Binary file ({entry.Length} bytes)";
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        catch
        {
            return "// Error reading file";
        }
    }

    static bool IsBinary(string path)
    {
        string[] exts = { ".class", ".jar" };
        return exts.Any(e => path.EndsWith(e, System.StringComparison.OrdinalIgnoreCase));
    }

    public static string DecompileClass(string jarPath, string classPath)
    {
        try
        {
            string tmp = Path.Combine(Path.GetTempPath(), "Mizu", Path.GetRandomFileName());
            Directory.CreateDirectory(tmp);
            string cls = Path.Combine(tmp, Path.GetFileName(classPath));
            string outDir = Path.Combine(tmp, "out");
            Directory.CreateDirectory(outDir);

            using (var arc = ZipFile.OpenRead(jarPath))
            {
                var entry = arc.GetEntry(classPath);
                if (entry == null) return "// Class not found";
                using var s = entry.Open();
                using var fs = File.Create(cls);
                s.CopyTo(fs);
            }

            string cfr = Path.Combine(AppContext.BaseDirectory, "assets", "external", "cfr.jar");
            if (!File.Exists(cfr)) return "// CFR not found";

            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{cfr}\" \"{cls}\" --outputdir \"{outDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            p?.WaitForExit();

            string baseName = Path.GetFileNameWithoutExtension(classPath);
            string f = Directory.GetFiles(outDir, $"{baseName}.java", SearchOption.AllDirectories).FirstOrDefault();
            return f != null ? File.ReadAllText(f, Encoding.UTF8) : "// Decompiled file not found";
        }
        catch
        {
            return "// Error decompiling class";
        }
    }
}