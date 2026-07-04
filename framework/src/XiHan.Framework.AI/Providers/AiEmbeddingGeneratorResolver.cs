#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AiEmbeddingGeneratorResolver
// Guid:b2c3d4e5-f6a7-4b07-9d07-1a1b1c1d1e07
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// 多 provider 嵌入生成器解析器（按名从配置源构建并缓存）
/// </summary>
/// <remarks>与 <c>AiChatClientResolver</c> 同构：按 provider 名缓存已构建的嵌入生成器，写后经 <see cref="Invalidate"/> 失效重建。</remarks>
public sealed class AiEmbeddingGeneratorResolver : IAiEmbeddingGeneratorResolver, IDisposable
{
    private const string DefaultKey = " default";

    private readonly IAiProviderConfigStore _configStore;
    private readonly OpenAiEmbeddingGeneratorFactory _factory;
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构造函数
    /// </summary>
    public AiEmbeddingGeneratorResolver(IAiProviderConfigStore configStore, OpenAiEmbeddingGeneratorFactory factory)
    {
        _configStore = configStore;
        _factory = factory;
    }

    /// <inheritdoc />
    public IEmbeddingGenerator<string, Embedding<float>> Resolve(string? providerName = null)
    {
        var cacheKey = string.IsNullOrWhiteSpace(providerName) ? DefaultKey : providerName;
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            var options = _configStore.GetAsync(providerName).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"未找到 AI Provider 配置:{providerName ?? "(默认)"}。请检查 XiHan:AI 配置或 provider 名。");
            return _factory.Create(options);
        });
    }

    /// <inheritdoc />
    public void Invalidate(string? providerName = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            foreach (var key in _cache.Keys.ToArray())
            {
                Remove(key);
            }

            return;
        }

        Remove(providerName);
        Remove(DefaultKey);
    }

    /// <summary>
    /// 释放缓存的可释放生成器
    /// </summary>
    public void Dispose()
    {
        foreach (var key in _cache.Keys.ToArray())
        {
            Remove(key);
        }
    }

    private void Remove(string key)
    {
        if (_cache.TryRemove(key, out var generator))
        {
            generator.Dispose();
        }
    }
}
