// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core Provider 选项。从配置节 <c>XiHan:Persistence:EntityFrameworkCore</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record EntityFrameworkCorePersistenceOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:EntityFrameworkCore";

    /// <summary>
    /// 启动时调用 <c>EnsureCreated</c> 建表。只用于开发与测试；生产用迁移。默认 <see langword="false"/>。
    /// </summary>
    public bool EnsureCreated { get; init; }

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static EntityFrameworkCorePersistenceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[$"{ConfigurationSection}:EnsureCreated"];
        if (raw is null)
        {
            return new EntityFrameworkCorePersistenceOptions();
        }

        return bool.TryParse(raw, out var ensureCreated)
            ? new EntityFrameworkCorePersistenceOptions { EnsureCreated = ensureCreated }
            : throw new ArgumentException($"'{ConfigurationSection}:EnsureCreated' must be 'true' or 'false', but was '{raw}'.");
    }
}
