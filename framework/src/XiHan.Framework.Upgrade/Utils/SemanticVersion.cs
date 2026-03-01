#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SemanticVersion
// Guid:1dcbcb1b-cc54-4b07-9b7e-7b2f6a2cc3bb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:25:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Utils;

/// <summary>
/// 语义化版本
/// </summary>
public readonly struct SemanticVersion : IComparable<SemanticVersion>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="major">主版本号</param>
    /// <param name="minor">次版本号</param>
    /// <param name="patch">修订号</param>
    public SemanticVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// 主版本号
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// 次版本号
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// 修订号
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// 尝试解析语义化版本字符串
    /// </summary>
    /// <param name="value">要解析的版本字符串</param>
    /// <param name="version">解析后的语义化版本</param>
    /// <returns>解析是否成功</returns>
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var preReleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (preReleaseIndex >= 0)
        {
            normalized = normalized[..preReleaseIndex];
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var major = ParsePart(parts, 0);
        var minor = ParsePart(parts, 1);
        var patch = ParsePart(parts, 2);
        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch);
        return true;
    }

    /// <summary>
    /// 比较两个版本字符串的大小关系
    /// </summary>
    /// <param name="left">左侧版本字符串</param>
    /// <param name="right">右侧版本字符串</param>
    /// <returns>比较结果，-1 表示左侧版本小于右侧版本，0 表示相等，1 表示左侧版本大于右侧版本</returns>
    public static int Compare(string? left, string? right)
    {
        if (!TryParse(left, out var leftVersion))
        {
            leftVersion = new SemanticVersion(0, 0, 0);
        }

        if (!TryParse(right, out var rightVersion))
        {
            rightVersion = new SemanticVersion(0, 0, 0);
        }

        return leftVersion.CompareTo(rightVersion);
    }

    /// <summary>
    /// 比较当前版本与另一个版本的大小关系
    /// </summary>
    /// <param name="other">要比较的另一个版本</param>
    /// <returns>比较结果，-1 表示当前版本小于另一个版本，0 表示相等，1 表示当前版本大于另一个版本</returns>
    public int CompareTo(SemanticVersion other)
    {
        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0)
        {
            return majorCompare;
        }

        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0)
        {
            return minorCompare;
        }

        return Patch.CompareTo(other.Patch);
    }

    /// <summary>
    /// 返回版本字符串表示
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }

    /// <summary>
    /// 解析版本字符串的指定部分
    /// </summary>
    /// <param name="parts">版本字符串的各部分</param>
    /// <param name="index">要解析的部分索引</param>
    /// <returns>解析后的整数值，如果解析失败返回 -1</returns>
    private static int ParsePart(string[] parts, int index)
    {
        if (index >= parts.Length)
        {
            return 0;
        }

        return int.TryParse(parts[index], out var value) ? value : -1;
    }
}
