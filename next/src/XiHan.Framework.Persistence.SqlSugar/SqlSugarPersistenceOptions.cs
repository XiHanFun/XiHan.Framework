// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar Provider 选项。从配置节 <c>XiHan:Persistence:SqlSugar</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SqlSugarPersistenceOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:SqlSugar";

    /// <summary>
    /// 启动时用 CodeFirst 为已登记的实体建表（<c>InitTables</c>）。只用于开发与测试；生产由应用管理表结构。默认 <see langword="false"/>。
    /// </summary>
    public bool InitTables { get; init; }

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static SqlSugarPersistenceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[$"{ConfigurationSection}:InitTables"];
        if (raw is null)
        {
            return new SqlSugarPersistenceOptions();
        }

        return bool.TryParse(raw, out var initTables)
            ? new SqlSugarPersistenceOptions { InitTables = initTables }
            : throw new ArgumentException($"'{ConfigurationSection}:InitTables' must be 'true' or 'false', but was '{raw}'.");
    }
}
