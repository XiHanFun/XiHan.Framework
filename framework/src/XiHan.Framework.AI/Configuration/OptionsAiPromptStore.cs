// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Prompts;

namespace XiHan.Framework.AI.Configuration;

/// <summary>
/// 默认提示词库：读 <see cref="IOptionsMonitor{XiHanAiOptions}"/> 的 Prompts（appsettings 兜底）
/// </summary>
/// <remarks>应用层用 SysAiPrompt 的 DB 实现经 <c>Replace</c> 覆盖(store 化 + 后台维护 + 版本),对上层透明。</remarks>
public sealed class OptionsAiPromptStore : IAiPromptStore
{
    private readonly IOptionsMonitor<XiHanAiOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OptionsAiPromptStore(IOptionsMonitor<XiHanAiOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<AiPromptTemplate?> GetAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        var prompts = _options.CurrentValue.Prompts;
        var match = prompts.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (version is null || string.Equals(p.Version, version, StringComparison.OrdinalIgnoreCase)));
        return Task.FromResult(match);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AiPromptTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AiPromptTemplate>>(_options.CurrentValue.Prompts.ToList());
    }
}
