#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAiOptions
// Guid:a1b2c3d4-e5f6-4a02-9c02-0a0b0c0d0e02
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Configuration;

/// <summary>
/// XiHan AI 根配置（默认 provider + 各 provider 配置；绑定配置节 <see cref="SectionName"/>）
/// </summary>
/// <remarks>
/// 这是「Options 兜底源」的形态；store 化（DB + 前端页 + 多租户）由应用层通过
/// <c>IAiProviderConfigStore</c> 覆盖实现，框架不强绑 DB。
/// </remarks>
public class XiHanAiOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:AI";

    /// <summary>
    /// 默认 provider 名（未显式指定 provider 时使用）
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// 各 provider 配置（键为 provider 名，大小写不敏感）
    /// </summary>
    public IDictionary<string, AiProviderOptions> Providers { get; set; }
        = new Dictionary<string, AiProviderOptions>(StringComparer.OrdinalIgnoreCase);
}
