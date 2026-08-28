// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
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

    /// <summary>
    /// 解析指定 provider 的嵌入生成器，为空取默认 provider，构建后按名缓存复用
    /// </summary>
    /// <param name="providerName">provider 配置名，为空取默认 provider</param>
    /// <returns>该 provider 的嵌入生成器</returns>
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

    /// <summary>
    /// 使已缓存的嵌入生成器失效并释放，下次解析按最新配置重建
    /// </summary>
    /// <param name="providerName">provider 配置名，为空则清空全部缓存，否则清该 provider 及默认槽</param>
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
