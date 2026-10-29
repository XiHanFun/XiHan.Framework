// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Ip2Region;

/// <summary>
/// ip2region 选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Security:Ip2Region</c> 显式读取。xdb 文件由宿主提供（框架不随包分发数据）；相对路径按 <see cref="AppContext.BaseDirectory"/> 解析。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record Ip2RegionOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:Ip2Region";

    /// <summary>
    /// IPv4 的 xdb 文件。
    /// </summary>
    /// <remarks>
    /// 默认 <c>IpDatabases/ip2region_v4.xdb</c>。文件不存在时 IPv4 地址不解析归属地。
    /// </remarks>
    public string V4Database
    {
        get => field;
        init => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("The IPv4 database path must not be empty.", nameof(value)) : value;
    } = Path.Combine("IpDatabases", "ip2region_v4.xdb");

    /// <summary>
    /// IPv6 的 xdb 文件。
    /// </summary>
    /// <remarks>
    /// 默认 <c>IpDatabases/ip2region_v6.xdb</c>。文件不存在时 IPv6 地址不解析归属地。
    /// </remarks>
    public string V6Database
    {
        get => field;
        init => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("The IPv6 database path must not be empty.", nameof(value)) : value;
    } = Path.Combine("IpDatabases", "ip2region_v6.xdb");

    /// <summary>
    /// 从配置读取：<c>V4Database</c>、<c>V6Database</c>。
    /// </summary>
    public static Ip2RegionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new Ip2RegionOptions();
        if (configuration[$"{ConfigurationSection}:V4Database"] is { } v4)
        {
            options = options with { V4Database = v4 };
        }

        if (configuration[$"{ConfigurationSection}:V6Database"] is { } v6)
        {
            options = options with { V6Database = v6 };
        }

        return options;
    }

    /// <summary>
    /// 把路径解析为绝对路径：已是绝对路径的原样返回，否则按 <see cref="AppContext.BaseDirectory"/> 拼接。
    /// </summary>
    internal static string Resolve(string path) => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
