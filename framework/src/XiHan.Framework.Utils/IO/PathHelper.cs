// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// 路径帮助类
/// </summary>
/// <remarks>
/// 提供路径验证、规范化、转换、分析等功能，
/// 支持跨平台路径操作和安全检查
/// </remarks>
public static class PathHelper
{
    #region 常量定义

    /// <summary>
    /// Windows 系统无效字符
    /// </summary>
    private static readonly char[] WindowsInvalidChars = ['<', '>', ':', '"', '|', '?', '*'];

    /// <summary>
    /// Windows 系统保留名称
    /// </summary>
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// 路径分隔符
    /// </summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// 本机文件系统路径的比较方式：Windows 与 macOS 默认不区分大小写，类 Unix 区分
    /// </summary>
    /// <remarks>
    /// 只用于比较<b>本机文件系统路径</b>——大小写规则本就由运行平台的文件系统决定，
    /// 硬编码 <see cref="StringComparison.OrdinalIgnoreCase"/> 会在 Linux 上把
    /// <c>/app/Data</c> 与 <c>/app/data</c> 这两个不同目录判成同一个。
    /// 净化后的文件名、对象存储的键、配置里的路径字符串这些是<b>数据</b>不是本机路径，
    /// 一律用 <see cref="StringComparison.Ordinal"/>，不要用这个。
    /// </remarks>
    public static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// 本机文件系统路径的比较器，语义同 <see cref="PathComparison"/>
    /// </summary>
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    /// 无效路径字符正则表达式
    /// </summary>
    private static readonly Regex InvalidPathCharsRegex = new(@"[<>:""/\\|?*\x00-\x1f]", RegexOptions.Compiled);

    /// <summary>
    /// 无效文件名字符正则表达式
    /// </summary>
    private static readonly Regex InvalidFileNameCharsRegex = new(@"[<>:""/\\|?*\x00-\x1f]", RegexOptions.Compiled);

    #endregion

    #region 路径验证

    /// <summary>
    /// 验证路径是否有效
    /// </summary>
    /// <param name="path">要验证的路径</param>
    /// <returns>路径是否有效</returns>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            // 检查路径长度
            if (path.Length > GetMaxPathLength())
            {
                return false;
            }

            // 检查无效字符
            if (ContainsInvalidPathChars(path))
            {
                return false;
            }

            // 尝试获取完整路径来验证格式
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证文件名是否有效
    /// </summary>
    /// <param name="fileName">要验证的文件名</param>
    /// <returns>文件名是否有效</returns>
    public static bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // 检查长度
        if (fileName.Length > 255)
        {
            return false;
        }

        // 检查无效字符
        if (ContainsInvalidFileNameChars(fileName))
        {
            return false;
        }

        // 保留名与末尾点/空格是 Windows 的限制，但判定不看运行平台：
        // 同一个文件名在两个平台给出相反结论，会让「本地能建、部署后建不了」这类问题晚一步才暴露
        var nameWithoutExtension = GetFileNameWithoutExtensionOrdinal(fileName).ToUpperInvariant();
        return !WindowsReservedNames.Contains(nameWithoutExtension) &&
               !fileName.EndsWith('.') &&
               !fileName.EndsWith(' ');
    }

    /// <summary>
    /// 检查路径是否包含无效字符
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否包含无效字符</returns>
    public static bool ContainsInvalidPathChars(string path)
    {
        return InvalidPathCharsRegex.IsMatch(path);
    }

    /// <summary>
    /// 检查文件名是否包含无效字符
    /// </summary>
    /// <param name="fileName">要检查的文件名</param>
    /// <returns>是否包含无效字符</returns>
    public static bool ContainsInvalidFileNameChars(string fileName)
    {
        return InvalidFileNameCharsRegex.IsMatch(fileName);
    }

    /// <summary>
    /// 验证路径是否安全（防止路径遍历攻击）
    /// </summary>
    /// <param name="path">要验证的路径</param>
    /// <param name="basePath">基础路径</param>
    /// <returns>路径是否安全</returns>
    public static bool IsPathSafe(string path, string basePath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(basePath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(basePath, path));
            var fullBasePath = Path.GetFullPath(basePath);

            return IsWithin(fullBasePath, fullPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断一个已展开的绝对路径是否落在基准目录之内（含基准目录自身）
    /// </summary>
    /// <remarks>
    /// 必须卡在分隔符边界上：裸前缀匹配会把 <c>/app/database/secret</c> 判成 <c>/app/data</c> 的子路径，
    /// 目录穿越就是这么放行的。大小写按运行平台的文件系统语义判定，见 <see cref="PathComparison"/>。
    /// </remarks>
    /// <param name="fullBasePath">基准目录的绝对路径</param>
    /// <param name="fullPath">待判定的绝对路径</param>
    /// <returns>落在基准目录之内返回 true</returns>
    private static bool IsWithin(string fullBasePath, string fullPath)
    {
        var normalizedBase = fullBasePath.TrimEnd(PathSeparators);
        var normalizedPath = fullPath.TrimEnd(PathSeparators);

        // 基准目录是根（TrimEnd 后为空或形如 C:）时，同盘任何路径都在其内
        if (normalizedBase.Length == 0)
        {
            return true;
        }

        if (string.Equals(normalizedBase, normalizedPath, PathComparison))
        {
            return true;
        }

        return normalizedPath.Length > normalizedBase.Length &&
               normalizedPath.StartsWith(normalizedBase, PathComparison) &&
               PathSeparators.Contains(normalizedPath[normalizedBase.Length]);
    }

    #endregion

    #region 路径规范化

    /// <summary>
    /// 规范化路径（统一分隔符、移除多余分隔符等）
    /// </summary>
    /// <param name="path">要规范化的路径</param>
    /// <param name="preferredSeparator">首选的路径分隔符</param>
    /// <returns>规范化后的路径</returns>
    public static string NormalizePath(string path, char? preferredSeparator = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        preferredSeparator ??= Path.DirectorySeparatorChar;

        // 替换所有路径分隔符为首选分隔符
        var normalized = path.Replace('\\', preferredSeparator.Value).Replace('/', preferredSeparator.Value);

        // 移除多余的分隔符
        while (normalized.Contains($"{preferredSeparator}{preferredSeparator}"))
        {
            normalized = normalized.Replace($"{preferredSeparator}{preferredSeparator}", preferredSeparator.ToString());
        }

        // 移除末尾的分隔符（除非是根目录）
        if (normalized.Length > 1 && normalized.EndsWith(preferredSeparator.Value))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    /// <summary>
    /// 标准化路径为 Unix 风格（使用正斜杠）
    /// </summary>
    /// <param name="path">要标准化的路径</param>
    /// <returns>Unix 风格的路径</returns>
    public static string ToUnixPath(string path)
    {
        return NormalizePath(path, '/');
    }

    /// <summary>
    /// 标准化路径为 Windows 风格（使用反斜杠）
    /// </summary>
    /// <param name="path">要标准化的路径</param>
    /// <returns>Windows 风格的路径</returns>
    public static string ToWindowsPath(string path)
    {
        return NormalizePath(path, '\\');
    }

    /// <summary>
    /// 清理文件名中的无效字符
    /// </summary>
    /// <param name="fileName">要清理的文件名</param>
    /// <param name="replacement">替换字符</param>
    /// <returns>清理后的文件名</returns>
    public static string SanitizeFileName(string fileName, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        // 按各平台限制的并集净化，不看运行平台：净化产物要落库、进 URL、跨平台流转，
        // 用 Path.GetInvalidFileNameChars() 会让同一个文件名在 Windows（41 个非法字符）
        // 与 Linux（只有 \0 和 /）上被清洗成两个不同的字符串
        var sanitized = InvalidFileNameCharsRegex.Replace(fileName, replacement.ToString());

        // 保留名与末尾点/空格是 Windows 的限制，但同样无条件处理：
        // 取最严格的规则，产物才能在任何平台上都是合法文件名
        var nameWithoutExtension = GetFileNameWithoutExtensionOrdinal(sanitized);
        if (WindowsReservedNames.Contains(nameWithoutExtension.ToUpperInvariant()))
        {
            sanitized = $"{nameWithoutExtension}{replacement}{GetExtensionOrdinal(sanitized)}";
        }

        sanitized = sanitized.TrimEnd('.', ' ');

        // 确保文件名不为空
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "file";
        }

        return sanitized;
    }

    #endregion

    #region 路径转换

    /// <summary>
    /// 获取相对于基础路径的相对路径
    /// </summary>
    /// <param name="fullPath">完整路径</param>
    /// <param name="basePath">基础路径</param>
    /// <returns>相对路径</returns>
    public static string GetRelativePath(string fullPath, string basePath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(basePath))
        {
            return fullPath;
        }

        try
        {
            return Path.GetRelativePath(basePath, fullPath);
        }
        catch
        {
            return fullPath;
        }
    }

    /// <summary>
    /// 将相对路径转换为绝对路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <param name="basePath">基础路径</param>
    /// <returns>绝对路径</returns>
    public static string GetAbsolutePath(string relativePath, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        try
        {
            if (Path.IsPathRooted(relativePath))
            {
                return Path.GetFullPath(relativePath);
            }

            basePath ??= Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(basePath, relativePath));
        }
        catch
        {
            return relativePath;
        }
    }

    /// <summary>
    /// 安全地合并路径
    /// </summary>
    /// <param name="paths">要合并的路径数组</param>
    /// <returns>合并后的路径</returns>
    public static string CombinePaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var result = paths[0];
            for (var i = 1; i < paths.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(paths[i]))
                {
                    result = Path.Combine(result, paths[i]);
                }
            }
            return NormalizePath(result);
        }
        catch
        {
            return string.Join(Path.DirectorySeparatorChar, paths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    #endregion

    #region 路径分析

    /// <summary>
    /// 获取路径的所有组件
    /// </summary>
    /// <param name="path">要分析的路径</param>
    /// <returns>路径组件信息</returns>
    public static PathComponents GetPathComponents(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new PathComponents();
        }

        try
        {
            return new PathComponents
            {
                FullPath = Path.GetFullPath(path),
                DirectoryName = Path.GetDirectoryName(path),
                FileName = Path.GetFileName(path),
                FileNameWithoutExtension = Path.GetFileNameWithoutExtension(path),
                Extension = Path.GetExtension(path),
                Root = Path.GetPathRoot(path),
                IsAbsolute = Path.IsPathRooted(path),
                Exists = File.Exists(path) || Directory.Exists(path),
                IsFile = File.Exists(path),
                IsDirectory = Directory.Exists(path)
            };
        }
        catch
        {
            return new PathComponents
            {
                FullPath = path,
                DirectoryName = null,
                FileName = path,
                FileNameWithoutExtension = path,
                Extension = string.Empty,
                Root = null,
                IsAbsolute = false,
                Exists = false,
                IsFile = false,
                IsDirectory = false
            };
        }
    }

    /// <summary>
    /// 获取路径深度（目录层级数）
    /// </summary>
    /// <param name="path">要分析的路径</param>
    /// <returns>路径深度</returns>
    public static int GetPathDepth(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return 0;
        }

        var normalizedPath = NormalizePath(path);
        var parts = normalizedPath.Split([Path.DirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        // 如果是绝对路径，减去根部分
        if (Path.IsPathRooted(normalizedPath))
        {
            return parts.Length - 1;
        }

        return parts.Length;
    }

    /// <summary>
    /// 获取两个路径的公共前缀路径
    /// </summary>
    /// <param name="path1">第一个路径</param>
    /// <param name="path2">第二个路径</param>
    /// <returns>公共前缀路径</returns>
    public static string GetCommonPath(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
        {
            return string.Empty;
        }

        var parts1 = NormalizePath(path1).Split(Path.DirectorySeparatorChar);
        var parts2 = NormalizePath(path2).Split(Path.DirectorySeparatorChar);

        var commonParts = new List<string>();
        var minLength = Math.Min(parts1.Length, parts2.Length);

        for (var i = 0; i < minLength; i++)
        {
            if (string.Equals(parts1[i], parts2[i], PathComparison))
            {
                commonParts.Add(parts1[i]);
            }
            else
            {
                break;
            }
        }

        return commonParts.Count > 0 ? string.Join(Path.DirectorySeparatorChar, commonParts) : string.Empty;
    }

    #endregion

    #region 路径比较

    /// <summary>
    /// 比较两个路径是否相等（忽略大小写和路径分隔符差异）
    /// </summary>
    /// <param name="path1">第一个路径</param>
    /// <param name="path2">第二个路径</param>
    /// <returns>路径是否相等</returns>
    public static bool PathEquals(string? path1, string? path2)
    {
        if (path1 == null && path2 == null)
        {
            return true;
        }

        if (path1 == null || path2 == null)
        {
            return false;
        }

        var normalized1 = NormalizePath(path1);
        var normalized2 = NormalizePath(path2);

        return string.Equals(normalized1, normalized2, PathComparison);
    }

    /// <summary>
    /// 检查一个路径是否是另一个路径的子路径
    /// </summary>
    /// <param name="parentPath">父路径</param>
    /// <param name="childPath">子路径</param>
    /// <returns>是否为子路径</returns>
    public static bool IsSubPath(string parentPath, string childPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(childPath))
        {
            return false;
        }

        try
        {
            return IsWithin(Path.GetFullPath(parentPath), Path.GetFullPath(childPath));
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 特殊路径处理

    /// <summary>
    /// 展开路径中的环境变量
    /// </summary>
    /// <param name="path">包含环境变量的路径</param>
    /// <returns>展开后的路径</returns>
    public static string ExpandEnvironmentVariables(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    /// <summary>
    /// 获取用户主目录路径
    /// </summary>
    /// <returns>用户主目录路径</returns>
    public static string GetUserHomePath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>
    /// 获取临时目录路径
    /// </summary>
    /// <returns>临时目录路径</returns>
    public static string GetTempPath()
    {
        return Path.GetTempPath();
    }

    /// <summary>
    /// 获取应用程序数据目录路径
    /// </summary>
    /// <returns>应用程序数据目录路径</returns>
    public static string GetAppDataPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    /// <summary>
    /// 创建唯一的临时文件路径
    /// </summary>
    /// <param name="extension">文件扩展名</param>
    /// <returns>唯一的临时文件路径</returns>
    public static string GetTempFileName(string? extension = null)
    {
        var tempPath = GetTempPath();
        var fileName = Path.GetRandomFileName();

        if (!string.IsNullOrWhiteSpace(extension))
        {
            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }
            fileName = Path.ChangeExtension(fileName, extension);
        }

        return Path.Combine(tempPath, fileName);
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 获取当前平台的最大路径长度
    /// </summary>
    /// <returns>最大路径长度</returns>
    public static int GetMaxPathLength()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 260 : 4096;
    }

    /// <summary>
    /// 获取当前平台的最大文件名长度
    /// </summary>
    /// <returns>最大文件名长度</returns>
    public static int GetMaxFileNameLength()
    {
        return 255; // 大多数文件系统的限制
    }

    /// <summary>
    /// 检查路径是否为UNC路径（网络路径）
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否为UNC路径</returns>
    public static bool IsUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.StartsWith(@"\\") || path.StartsWith("//");
    }

    /// <summary>
    /// 获取路径的哈希值（用于路径比较和缓存）
    /// </summary>
    /// <param name="path">要计算哈希的路径</param>
    /// <returns>路径的哈希值</returns>
    public static string GetPathHash(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        // 分隔符统一成 '/'（不取平台分隔符），大小写按平台的文件系统语义折叠：
        // 同一个路径在同一台机器上必须得到同一个哈希，而 Windows 上 C:\A 与 C:\a 就是同一个路径
        var normalizedPath = NormalizePath(path, '/');
        if (PathComparison == StringComparison.OrdinalIgnoreCase)
        {
            normalizedPath = normalizedPath.ToLowerInvariant();
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 截断路径到指定长度（保持可读性）
    /// </summary>
    /// <param name="path">要截断的路径</param>
    /// <param name="maxLength">最大长度</param>
    /// <param name="ellipsis">省略号字符串</param>
    /// <returns>截断后的路径</returns>
    public static string TruncatePath(string path, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length <= maxLength)
        {
            return path;
        }

        if (maxLength <= ellipsis.Length)
        {
            return ellipsis[..maxLength];
        }

        var components = GetPathComponents(path);
        if (!string.IsNullOrEmpty(components.FileName))
        {
            var fileNameLength = components.FileName.Length;
            var availableLength = maxLength - ellipsis.Length - fileNameLength - 1; // -1 for separator

            if (availableLength > 0 && !string.IsNullOrEmpty(components.DirectoryName))
            {
                var truncatedDir = components.DirectoryName.Length > availableLength
                    ? components.DirectoryName[..availableLength]
                    : components.DirectoryName;

                return $"{truncatedDir}{ellipsis}{Path.DirectorySeparatorChar}{components.FileName}";
            }
        }

        // 如果无法智能截断，直接截断
        return path[..(maxLength - ellipsis.Length)] + ellipsis;
    }

    #endregion

    #region 平台无关的名称拆分

    /// <summary>
    /// 取文件名中扩展名之前的部分，两种分隔符都认，不看运行平台
    /// </summary>
    /// <remarks>
    /// <see cref="Path.GetFileNameWithoutExtension(string)"/> 按运行平台的分隔符语义拆分，
    /// 处理「要跨平台流转的名字」时会给出不同结果，故此处自行按字符拆。
    /// </remarks>
    /// <param name="fileName">文件名或路径</param>
    /// <returns>扩展名之前的部分</returns>
    private static string GetFileNameWithoutExtensionOrdinal(string fileName)
    {
        var name = GetFileNameOrdinal(fileName);
        var extensionIndex = name.LastIndexOf('.');
        return extensionIndex > 0 ? name[..extensionIndex] : name;
    }

    /// <summary>
    /// 取文件名的扩展名（含点），两种分隔符都认，不看运行平台
    /// </summary>
    /// <param name="fileName">文件名或路径</param>
    /// <returns>扩展名，无扩展名时为空串</returns>
    private static string GetExtensionOrdinal(string fileName)
    {
        var name = GetFileNameOrdinal(fileName);
        var extensionIndex = name.LastIndexOf('.');
        return extensionIndex > 0 ? name[extensionIndex..] : string.Empty;
    }

    /// <summary>
    /// 取路径最后一段，两种分隔符都认，不看运行平台
    /// </summary>
    /// <param name="path">文件名或路径</param>
    /// <returns>最后一段</returns>
    private static string GetFileNameOrdinal(string path)
    {
        var separatorIndex = path.LastIndexOfAny(PathSeparators);
        return separatorIndex < 0 ? path : path[(separatorIndex + 1)..];
    }

    #endregion
}

/// <summary>
/// 路径组件信息
/// </summary>
public record PathComponents
{
    /// <summary>
    /// 完整路径
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// 目录名
    /// </summary>
    public string? DirectoryName { get; set; }

    /// <summary>
    /// 文件名（包含扩展名）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件名（不包含扩展名）
    /// </summary>
    public string FileNameWithoutExtension { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// 根路径
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// 是否为绝对路径
    /// </summary>
    public bool IsAbsolute { get; set; }

    /// <summary>
    /// 路径是否存在
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 是否为文件
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的路径信息</returns>
    public override string ToString()
    {
        var type = IsFile ? "文件" : IsDirectory ? "目录" : "路径";
        return $"{type}: {FullPath}";
    }
}
