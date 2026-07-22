// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Configuration;

/// <summary>
/// AI Provider 配置来源抽象（可插拔）
/// </summary>
/// <remarks>
/// 框架不强绑配置来源：默认实现读 <c>IOptionsMonitor&lt;XiHanAiOptions&gt;</c>（appsettings 兜底）；
/// 应用层（如 BasicApp.Saas）用 SysAiProvider 实体 + DataProtection 提供 DB/store 实现，
/// 经 <c>TryAddSingleton</c> 覆盖，对上层透明（Options 还是 DB 无差别）。沿用 IEmailConfigStore 范式。
/// </remarks>
public interface IAiProviderConfigStore
{
    /// <summary>
    /// 取指定 provider 的生效配置（null 取默认 provider）；无匹配返回 null（调用方 fail-closed 处理）
    /// </summary>
    Task<AiProviderOptions?> GetAsync(string? providerName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取全部启用 provider 配置
    /// </summary>
    Task<IReadOnlyList<AiProviderOptions>> GetAllAsync(CancellationToken cancellationToken = default);
}
