using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Xml;
using System.Collections.Generic;

namespace Mizu;

public partial class MainWindow : Window
{
    string? jarPath;
    bool maximized;
    List<TreeViewItem>? OriginalTree;

    public MainWindow()
    {
        InitializeComponent();
        LoadIcons();
        LoadCustomJavaTheme();
        Loaded += AnimateIntro;
    }

    void AnimateIntro(object s, RoutedEventArgs e)
    {
        var a = new DoubleAnimation(0, 1, new Duration(System.TimeSpan.FromSeconds(0.6)));
        BeginAnimation(OpacityProperty, a);
    }

    void LoadIcons()
    {
        string basePath = Path.Combine(AppContext.BaseDirectory, "assets", "external", "icons");
        void SetIcon(Image img, string file)
        {
            var p = Path.Combine(basePath, file);
            if (File.Exists(p))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new System.Uri(p, System.UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                img.Source = bmp;
            }
        }
        SetIcon(IconLogo, "Mizu.png");
        SetIcon(IconFile, "extension.png");
        SetIcon(IconCode, "code_page.png");
        SetIcon(IconFilter, "filter.png");
        SetIcon(IconFull, "full.png");
        SetIcon(IconClose, "close.png");
    }

    void LoadCustomJavaTheme()
    {
        var t = Path.Combine(AppContext.BaseDirectory, "assets", "external", "themes", "MizuJava.xshd");
        if (!File.Exists(t)) return;
        using var reader = XmlReader.Create(t);
        var def = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting("MizuJava", new[] { ".java" }, def);
        CodeViewer.SyntaxHighlighting = def;
    }

    void OpenJarClick(object s, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "JAR files (*.jar)|*.jar" };
        if (d.ShowDialog() == true)
        {
            jarPath = d.FileName;
            LoadJar(jarPath);
        }
    }

    void LoadJar(string path)
    {
        JarTree.Items.Clear();
        OriginalTree = JarProcessor.ExtractStructure(path);
        foreach (var item in OriginalTree)
            JarTree.Items.Add(item);
    }

    void FilterBox_TextChanged(object s, TextChangedEventArgs e)
    {
        if (OriginalTree == null) return;
        string query = FilterBox.Text.Trim().ToLower();
        JarTree.Items.Clear();

        if (string.IsNullOrEmpty(query))
        {
            foreach (var node in OriginalTree)
                JarTree.Items.Add(node);
            return;
        }

        var filtered = new List<TreeViewItem>();
        foreach (var node in OriginalTree)
        {
            var result = FilterTree(node, query);
            if (result != null) filtered.Add(result);
        }
        foreach (var n in filtered)
            JarTree.Items.Add(n);
    }

    TreeViewItem? FilterTree(TreeViewItem node, string query)
    {
        bool match = node.Header.ToString()!.ToLower().Contains(query);
        var matches = new List<TreeViewItem>();
        foreach (TreeViewItem child in node.Items)
        {
            var res = FilterTree(child, query);
            if (res != null) matches.Add(res);
        }
        if (match || matches.Count > 0)
        {
            var clone = new TreeViewItem { Header = node.Header, Tag = node.Tag };
            foreach (var m in matches) clone.Items.Add(m);
            return clone;
        }
        return null;
    }

    void JarTree_SelectedItemChanged(object s, RoutedPropertyChangedEventArgs<object> e)
    {
        if (jarPath == null) return;
        if (e.NewValue is not TreeViewItem item) return;
        if (item.Tag is not string entryPath || string.IsNullOrWhiteSpace(entryPath)) return;

        string ext = System.IO.Path.GetExtension(entryPath).ToLower();

        if (ext == ".class")
        {
            ShowCode(JarProcessor.DecompileClass(jarPath, entryPath));
            return;
        }

        if (JarProcessor.IsImage(entryPath))
        {
            var img = JarProcessor.ExtractImage(jarPath, entryPath);
            if (img != null)
                ShowImage(img);
            else
                ShowCode("// Image could not be loaded");
            return;
        }

        if (ext == ".jar")
        {
            if (item.Items.Count == 0)
            {
                var sub = JarProcessor.ExtractStructureFromInnerJar(jarPath, entryPath);
                if (sub != null)
                {
                    foreach (var node in sub)
                        item.Items.Add(node);
                    item.IsExpanded = true;
                }
                else
                {
                    ShowCode("// Could not open inner JAR");
                }
            }
            return;
        }

        ShowCode(JarProcessor.ReadTextFromJar(jarPath, entryPath));
    }

    void ShowCode(string text)
    {
        ImageViewer.Visibility = Visibility.Collapsed;
        CodeViewer.Visibility = Visibility.Visible;
        CodeViewer.Text = text;
    }

    void ShowImage(BitmapImage? img)
    {
        if (img == null) return;
        CodeViewer.Visibility = Visibility.Collapsed;
        ImageViewer.Visibility = Visibility.Visible;
        ImageViewer.Source = img;
    }

    void DragWindow(object s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    void ToggleMaximize(object s, RoutedEventArgs e)
    {
        double targetScale = maximized ? 1.0 : 1.04;
        var scale = new System.Windows.Media.ScaleTransform(1, 1);
        Root.RenderTransformOrigin = new Point(0.5, 0.5);
        Root.RenderTransform = scale;

        var anim = new DoubleAnimation(targetScale, new Duration(System.TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            AutoReverse = true
        };
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);

        if (maximized)
        {
            WindowState = WindowState.Normal;
            Root.Margin = new Thickness(0);
            maximized = false;
        }
        else
        {
            WindowState = WindowState.Maximized;
            Root.Margin = new Thickness(6);
            maximized = true;
        }
    }

    void CloseApp(object s, RoutedEventArgs e) => Close();

    void OnDropJar(object s, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var jar = files.FirstOrDefault(f => f.EndsWith(".jar"));
        if (jar == null) return;
        jarPath = jar;
        LoadJar(jarPath);
    }

    void OnPreviewDragOver(object s, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.Copy;
    }
}