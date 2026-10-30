// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Security;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 单项读取的容错。
/// </summary>
/// <remarks>
/// 权限不足、文件缺失、平台不支持与格式不对都让该项为空（RFC-0023），其他异常照常抛出。
/// </remarks>
internal static class Readings
{
    /// <summary>
    /// 读一个值，失败为空。
    /// </summary>
    public static T? Optional<T>(Func<T> read)
        where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 读一段文本，失败、空白为空。
    /// </summary>
    public static string? Text(Func<string?> read)
    {
        try
        {
            return Clean(read());
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 去掉首尾空白与结尾的空字符。
    /// </summary>
    /// <remarks>
    /// 结果为空串时为空。
    /// </remarks>
    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('\0').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// 是否属于"读不到"。
    /// </summary>
    public static bool IsUnreadable(Exception exception)
        => exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or InvalidOperationException or Win32Exception or NetworkInformationException or FormatException or OverflowException;
}
