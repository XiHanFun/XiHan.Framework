// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Network;

/// <summary>
/// IP 归属地。各段可能缺失（离线库里记为未知的段为 <see langword="null"/>）。
/// </summary>
/// <param name="Country">国家。</param>
/// <param name="Province">省份或一级行政区。</param>
/// <param name="City">城市。</param>
/// <param name="Isp">运营商。</param>
/// <param name="CountryCode">两位国家代码（ISO 3166-1 alpha-2）；数据源没有时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record IpRegion(string? Country, string? Province, string? City, string? Isp, string? CountryCode)
{
    /// <summary>
    /// 展示用的归属地文本：国家、省份、城市、运营商去掉缺失与重复（不区分大小写）后以空格连接。
    /// </summary>
    public string Location => string.Join(' ', new[] { Country, Province, City, Isp }.Where(static part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
}
