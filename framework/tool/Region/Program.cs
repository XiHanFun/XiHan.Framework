// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private const string RootDirectoryName = "XiHanFun";

    /// <summary>
    /// 统一的两行文件头（每行以换行结尾）。
    /// </summary>
    private const string HeaderText =
        "// Copyright (c) 2021-Present XiHanFun and contributors.\n" +
        "// Licensed under the MIT License. See LICENSE in the project root for license information.\n";

    /// <summary>
    /// 自定义 VS 模板文件，不参与文件头处理。（相对 XiHanFun 根目录的路径）
    /// </summary>
    private static readonly HashSet<string> ExcludedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "XiHan.Framework\\framework\\scripts\\editor\\Class.cs",
        "XiHan.Framework\\framework\\scripts\\editor\\Controller.cs",
        "XiHan.Framework\\framework\\scripts\\editor\\Interface.cs",
    };

    /// <summary>
    /// 旧版 #region 版权头（历史遗留，含尾随空行）。
    /// </summary>
    private static readonly Regex OldRegionHeaderRegex = new(
        @"\A\s*#region\s+<<版权版本注释>>[\s\S]*?#endregion\s+<<版权版本注释>>[ \t]*\r?\n(\r?\n)?",
        RegexOptions.Compiled);

    /// <summary>
    /// 现行两行文件头（含尾随空行），用于幂等重复运行。
    /// </summary>
    private static readonly Regex NewHeaderRegex = new(
        @"\A// Copyright \(c\) 2021-Present XiHanFun and contributors\.\r?\n// Licensed under the MIT License\.[^\r\n]*\r?\n(\r?\n)?",
        RegexOptions.Compiled);

    /// <summary>
    /// 保存文件时使用 UTF-8 无 BOM。
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static int _processedCount;
    private static int _updatedCount;
    private static int _insertedCount;

    private static void Main()
    {
        var root = FindXiHanFunRoot();
        Console.WriteLine($"目标根目录：{root}\n");

        var allCsFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBinOrObj(f))
            .ToList();
        var csFiles = allCsFiles
            .Where(f => !IsExcludedFile(root, f))
            .ToList();

        Console.WriteLine($"找到 {csFiles.Count} 个 C# 文件（已排除 {allCsFiles.Count - csFiles.Count} 个模板文件）");
        Console.WriteLine("开始处理...\n");

        foreach (var file in csFiles)
        {
            try
            {
                ProcessFile(file);
                _processedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理失败：{file}");
                Console.WriteLine($"错误：{ex.Message}\n");
            }
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("处理完成！统计信息：");
        Console.WriteLine($"  总文件数：{csFiles.Count}");
        Console.WriteLine($"  成功处理：{_processedCount}");
        Console.WriteLine($"  更新头部：{_updatedCount}");
        Console.WriteLine($"  插入头部：{_insertedCount}");
        Console.WriteLine(new string('=', 50));
        Console.ReadKey();
    }

    /// <summary>
    /// 从当前目录向上查找名为 XiHanFun 的目录并作为目标根路径；未找到则抛出异常。
    /// </summary>
    private static string FindXiHanFunRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (string.Equals(current.Name, RootDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"未找到名为 \"{RootDirectoryName}\" 的目录。请从 {RootDirectoryName} 或其子目录下运行本工具。");
    }

    /// <summary>
    /// 判断是否位于 bin/obj 目录。
    /// </summary>
    private static bool IsInBinOrObj(string fullPath)
    {
        var normalized = fullPath.Replace('/', '\\');
        return normalized.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断是否为排除的 VS 模板文件（按相对路径匹配）。
    /// </summary>
    private static bool IsExcludedFile(string root, string fullPath)
    {
        var rootDir = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = fullPath.Length == rootDir.Length
            ? string.Empty
            : fullPath[(rootDir.Length + 1)..];
        var normalized = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return ExcludedRelativePaths.Contains(normalized);
    }

    /// <summary>
    /// 规范化文件头：移除旧版 #region 头或现行两行头，再统一写入标准两行头。
    /// 输出统一为 LF、tab 转 4 空格、末尾换行、UTF-8 无 BOM。
    /// </summary>
    private static void ProcessFile(string filePath)
    {
        var original = File.ReadAllText(filePath, Encoding.UTF8);
        var content = original.Replace("\r\n", "\n").Replace("\r", "\n");

        bool hadHeader;
        if (OldRegionHeaderRegex.IsMatch(content))
        {
            content = OldRegionHeaderRegex.Replace(content, string.Empty, 1);
            hadHeader = true;
        }
        else if (NewHeaderRegex.IsMatch(content))
        {
            content = NewHeaderRegex.Replace(content, string.Empty, 1);
            hadHeader = true;
        }
        else
        {
            hadHeader = false;
        }

        content = HeaderText + "\n" + content;
        content = content.Replace("\t", "    ");
        if (!content.EndsWith('\n'))
        {
            content += "\n";
        }

        if (content == original)
        {
            return;
        }

        File.WriteAllText(filePath, content, Utf8NoBom);
        if (hadHeader)
        {
            _updatedCount++;
            Console.WriteLine($"更新头部：{Path.GetFileName(filePath)}");
        }
        else
        {
            _insertedCount++;
            Console.WriteLine($"插入头部：{Path.GetFileName(filePath)}");
        }
    }
}
