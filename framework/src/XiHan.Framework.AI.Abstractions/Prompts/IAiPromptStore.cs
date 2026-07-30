// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Prompts;

/// <summary>
/// 提示词库抽象（可插拔）
/// </summary>
/// <remarks>
/// 框架给抽象；默认实现可读内置/内存，应用层覆盖为 store 化（DB + 后台维护 + 版本），
/// 沿用 IAiProviderConfigStore 同款「框架抽象、应用实现」的可插拔范式。
/// </remarks>
public interface IAiPromptStore
{
    /// <summary>
    /// 取模板（version 为空取当前版本），无则返回 null
    /// </summary>
    Task<AiPromptTemplate?> GetAsync(string name, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出全部模板
    /// </summary>
    Task<IReadOnlyList<AiPromptTemplate>> ListAsync(CancellationToken cancellationToken = default);
}
