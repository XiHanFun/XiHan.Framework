#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Program
// Guid:76b26c08-d7c6-427c-a70f-67ac89457bfa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/01 14:06:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private const string RegionName = "<<版权版本注释>>";
    private const string RootDirectoryName = "XiHanFun";
    private const int HeaderLineCount = 12;

    /// <summary>
    /// 自定义 VS 模板文件，不参与版权头处理。
    /// </summary>
    private static readonly HashSet<string> ExcludedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "scripts\\editor\\Class.cs",
        "scripts\\editor\\Controller.cs",
        "scripts\\editor\\Interface.cs",
    };

    private static readonly Regex HeaderRegex = new(
        @"^\s*#region\s+<<版权版本注释>>[\s\S]*?#endregion\s+<<版权版本注释>>\r?\n\r?\n?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// 保存文件时使用 UTF-8 无 BOM（charset = utf-8）。
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static readonly HashSet<Guid> UsedGuids = new();
    private static int processedCount = 0;
    private static int updatedCount = 0;
    private static int rebuiltCount = 0;
    private static int insertedCount = 0;

    private static void Main()
    {
        var root = FindXiHanFunRoot();
        Console.WriteLine($"目标根目录：{root}\n");

        var allCsFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        var csFiles = allCsFiles
            .Where(f => !IsExcludedFile(root, f))
            .ToList();

        Console.WriteLine($"找到 {csFiles.Count} 个 C# 文件（已排除 {allCsFiles.Length - csFiles.Count} 个模板文件）");
        Console.WriteLine("开始处理...\n");

        // 第一遍：收集所有现有的有效 GUID
        Console.WriteLine("第一步：收集现有 GUID...");
        foreach (var file in csFiles)
        {
            try
            {
                CollectExistingGuid(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"收集 GUID 失败：{file}");
                Console.WriteLine($"错误：{ex.Message}\n");
            }
        }
        Console.WriteLine($"收集到 {UsedGuids.Count} 个唯一 GUID\n");

        // 第二遍：处理所有文件
        Console.WriteLine("第二步：处理文件...");
        foreach (var file in csFiles)
        {
            try
            {
                ProcessFile(file);
                processedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理失败：{file}");
                Console.WriteLine($"错误：{ex.Message}\n");
            }
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine($"处理完成！统计信息：");
        Console.WriteLine($"  总文件数：{csFiles.Count}");
        Console.WriteLine($"  成功处理：{processedCount}");
        Console.WriteLine($"  更新头部：{updatedCount}");
        Console.WriteLine($"  重建头部：{rebuiltCount}");
        Console.WriteLine($"  插入头部：{insertedCount}");
        Console.WriteLine($"  唯一 GUID 数：{UsedGuids.Count}");
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
    /// 收集文件中现有的有效 GUID
    /// </summary>
    private static void CollectExistingGuid(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var match = HeaderRegex.Match(content);

        if (!match.Success)
        {
            return;
        }

        var guidLine = match.Value
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault(l => l.Contains("Guid:"));

        if (guidLine == null)
        {
            return;
        }

        var guidStr = guidLine.Split(':', 2).Last().Trim();
        if (Guid.TryParse(guidStr, out var guid))
        {
            UsedGuids.Add(guid);
        }
    }

    private static void ProcessFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var createTime = GetCreateTime(filePath, content);
        var guid = GetOrGenerateGuid(content);

        if (HeaderRegex.IsMatch(content))
        {
            var oldHeader = HeaderRegex.Match(content).Value;

            if (IsHeaderValid(oldHeader, fileName))
            {
                var newHeader = UpdateHeader(oldHeader, fileName, guid, createTime);
                if (oldHeader != newHeader)
                {
                    content = content.Replace(oldHeader, newHeader);
                    WriteFileWithFormat(filePath, content);
                    updatedCount++;
                    Console.WriteLine($"更新头部：{Path.GetFileName(filePath)}");
                }
            }
            else
            {
                var newHeader = BuildHeader(fileName, guid, createTime);
                content = content.Replace(oldHeader, newHeader);
                WriteFileWithFormat(filePath, content);
                rebuiltCount++;
                Console.WriteLine($"重建头部：{Path.GetFileName(filePath)}");
            }
        }
        else
        {
            var header = BuildHeader(fileName, guid, createTime);
            content = header + content;
            WriteFileWithFormat(filePath, content);
            insertedCount++;
            Console.WriteLine($"插入头部：{Path.GetFileName(filePath)}");
        }
    }

    /// <summary>
    /// 按统一格式写入文件：charset = utf-8，end_of_line = lf，insert_final_newline = true，indent_style = space，indent_size = 4。
    /// </summary>
    private static void WriteFileWithFormat(string filePath, string content)
    {
        content = content
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
        if (!content.EndsWith('\n'))
        {
            content += "\n";
        }
        content = content.Replace("\t", "    ");
        File.WriteAllText(filePath, content, Utf8NoBom);
    }

    /// <summary>
    /// 获取或生成 GUID：优先使用现有的有效 GUID，否则生成新的唯一 GUID
    /// </summary>
    private static string GetOrGenerateGuid(string content)
    {
        // 尝试从现有头部获取 GUID
        var match = HeaderRegex.Match(content);
        if (match.Success)
        {
            var guidLine = match.Value
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .FirstOrDefault(l => l.Contains("Guid:"));

            if (guidLine != null)
            {
                var guidStr = guidLine.Split(':', 2).Last().Trim();
                if (Guid.TryParse(guidStr, out var existingGuid))
                {
                    // 如果现有 GUID 有效，直接返回
                    return guidStr;
                }
            }
        }

        // 生成新的唯一 GUID
        Guid newGuid;
        do
        {
            newGuid = Guid.NewGuid();
        } while (UsedGuids.Contains(newGuid));

        UsedGuids.Add(newGuid);
        return newGuid.ToString();
    }

    /// <summary>
    /// 按优先级获取创建时间：
    /// 1. 从文件头部的 CreateTime 字段获取
    /// 2. 从文件系统的创建时间获取
    /// 3. 使用当前时间
    /// </summary>
    private static string GetCreateTime(string filePath, string content)
    {
        // 尝试从现有头部获取 CreateTime
        var match = HeaderRegex.Match(content);
        if (match.Success)
        {
            var timeLine = match.Value
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .FirstOrDefault(l => l.Contains("CreateTime:"));

            if (timeLine != null)
            {
                var timeStr = timeLine.Split(':', 2).Last().Trim();
                if (DateTime.TryParse(timeStr, out var parsedTime))
                {
                    return parsedTime.ToString("yyyy/MM/dd HH:mm:ss");
                }
            }
        }

        // 尝试从文件系统获取创建时间
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists && fileInfo.CreationTime.Year > 1601) // 排除无效时间
            {
                return fileInfo.CreationTime.ToString("yyyy/MM/dd HH:mm:ss");
            }
        }
        catch
        {
            // 忽略文件系统访问错误
        }

        // 使用当前时间
        return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
    }

    /// <summary>
    /// 严格校验版权头是否完全符合规范
    /// </summary>
    private static bool IsHeaderValid(string header, string expectedFileName)
    {
        var lines = header
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(l => l.Length > 0)
            .ToArray();

        // 行数必须严格等于 12
        if (lines.Length != HeaderLineCount)
        {
            return false;
        }

        // Guid 行必须且只能有一条
        if (lines.Count(l => l.Contains("Guid:")) != 1)
        {
            return false;
        }

        // FileName 行必须且只能有一条
        if (lines.Count(l => l.Contains("FileName:")) != 1)
        {
            return false;
        }

        // CreateTime 行必须且只能有一条
        if (lines.Count(l => l.Contains("CreateTime:")) != 1)
        {
            return false;
        }

        // FileName 必须匹配
        var fileNameLine = lines.FirstOrDefault(l => l.Contains("FileName:"));
        if (fileNameLine == null || !fileNameLine.EndsWith(expectedFileName))
        {
            return false;
        }

        // Guid 必须合法且格式标准
        var guidLine = lines.FirstOrDefault(l => l.Contains("Guid:"));
        if (guidLine == null)
        {
            return false;
        }

        var guidStr = guidLine.Split(':', 2).Last().Trim();
        if (!Guid.TryParse(guidStr, out var parsedGuid))
        {
            return false;
        }

        // 验证 GUID 格式是否标准（小写，带连字符）
        if (guidStr != parsedGuid.ToString())
        {
            return false;
        }

        // CreateTime 必须可解析
        var timeLine = lines.FirstOrDefault(l => l.Contains("CreateTime:"));
        if (timeLine == null ||
            !DateTime.TryParse(timeLine.Split(':', 2).Last().Trim(), out _))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 更新现有头部（保持原有格式，只更新必要字段）
    /// </summary>
    private static string UpdateHeader(string header, string fileName, string guid, string createTime)
    {
        header = Regex.Replace(header,
            @"//\s*FileName\s*:.*",
            $"// FileName:{fileName}",
            RegexOptions.Compiled);

        header = Regex.Replace(header,
            @"//\s*Guid\s*:.*",
            $"// Guid:{guid}",
            RegexOptions.Compiled);

        header = Regex.Replace(header,
            @"//\s*CreateTime\s*:.*",
            $"// CreateTime:{createTime}",
            RegexOptions.Compiled);

        return header;
    }

    /// <summary>
    /// 构建标准格式的版权头部
    /// </summary>
    private static string BuildHeader(string fileName, string guid, string createTime)
    {
        return
$@"#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:{fileName}
// Guid:{guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:{createTime}
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

";
    }
}
