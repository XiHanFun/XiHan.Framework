// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Web.Core.Clients;

/// <summary>
/// IP 归属地
/// </summary>
/// <remarks>
/// 来自 ip2region 的记录。新版 xdb 是「国家|省份|城市|运营商|国家代码」，旧版是「国家|区域|省份|城市|运营商」，
/// <c>0</c> 表示未知。国内的名称是中文，国外是英文；港澳台的国家是「中国」，省份分别是「香港特别行政区」「澳门特别行政区」「台湾省」。
/// </remarks>
/// <param name="Country">国家</param>
/// <param name="Province">省份或一级行政区</param>
/// <param name="City">城市</param>
/// <param name="Isp">运营商</param>
/// <param name="CountryCode">两位国家代码（旧版 xdb 没有）</param>
public sealed record IpRegion(string? Country, string? Province, string? City, string? Isp, string? CountryCode)
{
    /// <summary>
    /// 展示用的归属地文本：各段去掉未知、去重后以空格连接
    /// </summary>
    public string Location => string.Join(' ', new[] { Country, Province, City, Isp, CountryCode }
        .Where(static part => !string.IsNullOrWhiteSpace(part))
        .Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 解析 ip2region 的记录文本
    /// </summary>
    /// <param name="record">记录文本</param>
    /// <param name="region">解析结果</param>
    /// <returns>有任一已知段时返回 <see langword="true"/>；保留地址（Reserved）与全部未知返回 <see langword="false"/></returns>
    public static bool TryParse(string? record, [NotNullWhen(true)] out IpRegion? region)
    {
        region = null;
        if (string.IsNullOrWhiteSpace(record))
        {
            return false;
        }

        var parts = record.Split('|').Select(static part => Known(part)).ToArray();
        if (parts.Length == 0 || string.Equals(parts[0], "Reserved", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? At(int index) => index < parts.Length ? parts[index] : null;
        var last = At(4);
        // 新版末段是两位大写的国家代码；旧版第二段是「区域」，多为未知
        region = last is { Length: 2 } && last.All(char.IsAsciiLetterUpper)
            ? new IpRegion(At(0), At(1), At(2), At(3), last)
            : new IpRegion(At(0), At(2), At(3), At(4), null);
        if (region is { Country: null, Province: null, City: null, Isp: null })
        {
            region = null;
            return false;
        }

        return true;
    }

    private static string? Known(string part)
    {
        var trimmed = part.Trim();
        return trimmed.Length == 0 || trimmed == "0" ? null : trimmed;
    }
}
