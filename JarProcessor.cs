using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace Mizu;

public static class JarProcessor
{
    public static List<TreeViewItem> ExtractStructure(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var root = new TreeViewItem { Header = Path.GetFileName(jarPath), Tag = null, IsExpanded = true };

        foreach (var entry in archive.Entries.OrderBy(e => e.FullName))
        {
            string[] parts = entry.FullName.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            TreeViewItem parent = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLast = i == parts.Length - 1;
                TreeViewItem? existing = parent.Items.OfType<TreeViewItem>().FirstOrDefault(x => (string)x.Header == part);
                if (existing == null)
                {
                    var node = new TreeViewItem { Header = part };
                    // если последний элемент пути и не «папка» — ставим тег
                    if (isLast && !entry.FullName.EndsWith("/"))
                        node.Tag = entry.FullName;
                    parent.Items.Add(node);
                    parent = node;
                }
                else
                {
                    parent = existing;
                }
            }
        }

        return new List<TreeViewItem> { root };
    }

    public static string ReadTextFromJar(string jarPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var entry = archive.Entries.FirstOrDefault(e => e.FullName == entryPath);
        if (entry == null) return "// File not found";

        using var stream = entry.Open();
        if (IsBinary(entry.FullName))
            return $"[{entry.FullName}] Binary file ({entry.Length} bytes)";

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    static bool IsBinary(string path)
    {
        string[] ext = { ".class", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".jar", ".wav", ".mp3" , ".ogg"};
        return ext.Any(e => path.EndsWith(e, System.StringComparison.OrdinalIgnoreCase));
    }

    public static string DecompileClass(string jarPath, string classPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Mizu");
        Directory.CreateDirectory(tempDir);
        var classFile = Path.Combine(tempDir, Path.GetFileName(classPath));
        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(outputDir);

        using (var archive = ZipFile.OpenRead(jarPath))
        {
            var entry = archive.GetEntry(classPath);
            if (entry == null) return "// Class not found";
            using var stream = entry.Open();
            using var fs = File.Create(classFile);
            stream.CopyTo(fs);
        }

        var cfrPath = Path.Combine(AppContext.BaseDirectory, "assets", "external", "cfr.jar");
        if (!File.Exists(cfrPath)) return "// CFR not found";

        var psi = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{cfrPath}\" \"{classFile}\" --outputdir \"{outputDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        proc?.WaitForExit();

        var baseName = Path.GetFileNameWithoutExtension(classPath);
        var javaFile = Directory.GetFiles(outputDir, baseName + ".java", SearchOption.AllDirectories).FirstOrDefault();
        return javaFile != null ? File.ReadAllText(javaFile, Encoding.UTF8) : "// Decompiled file not found";
    }
}