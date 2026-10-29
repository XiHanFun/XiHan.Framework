// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Network;

namespace XiHan.Framework.Security.Ip2Region;

/// <summary>
/// ip2region 记录文本的解析。
/// </summary>
/// <remarks>
/// 新版 xdb 是"国家|省份|城市|运营商|国家代码"，旧版是"国家|区域|省份|城市|运营商"；未知的段记为 <c>0</c> 或空，保留地址记为 <c>Reserved</c>。
/// </remarks>
internal static class Ip2RegionRecord
{
    /// <summary>
    /// 解析记录。
    /// </summary>
    /// <remarks>
    /// 有任一已知段时返回归属地，保留地址与全部未知返回 <see langword="null"/>。
    /// </remarks>
    public static IpRegion? Parse(string? record)
    {
        if (string.IsNullOrWhiteSpace(record))
        {
            return null;
        }

        var parts = record.Split('|').Select(static part => Known(part)).ToArray();
        if (string.Equals(parts[0], "Reserved", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? At(int index) => index < parts.Length ? parts[index] : null;

        // 新版末段是两位大写的国家代码；旧版第二段是区域，多为未知
        var last = At(4);
        var region = last is { Length: 2 } && last.All(char.IsAsciiLetterUpper)
            ? new IpRegion(At(0), At(1), At(2), At(3), last)
            : new IpRegion(At(0), At(2), At(3), At(4), null);
        return region is { Country: null, Province: null, City: null, Isp: null } ? null : region;
    }

    /// <summary>
    /// 一段的已知值：去空白，<c>0</c> 与空视为未知。
    /// </summary>
    private static string? Known(string part)
    {
        var trimmed = part.Trim();
        return trimmed.Length == 0 || trimmed == "0" ? null : trimmed;
    }
}
