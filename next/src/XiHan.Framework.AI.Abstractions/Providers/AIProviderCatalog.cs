// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// Provider 目录的实现（单例）：按名字找配置，按种类找工厂，实例按名字缓存；同一种类登记多个工厂以最后登记的为准。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIProviderCatalog : IAIProviderCatalog, IDisposable
{
    /// <summary>
    /// 保护缓存的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly AIOptions _options;

    /// <summary>
    /// 种类 → 聊天工厂。
    /// </summary>
    private readonly Dictionary<string, IChatClientFactory> _chatFactories;

    /// <summary>
    /// 种类 → 嵌入工厂。
    /// </summary>
    private readonly Dictionary<string, IEmbeddingGeneratorFactory> _embeddingFactories;

    /// <summary>
    /// 名字 → 聊天客户端。
    /// </summary>
    private readonly Dictionary<string, IChatClient> _chatClients = new(StringComparer.Ordinal);

    /// <summary>
    /// 名字 → 嵌入生成器。
    /// </summary>
    private readonly Dictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _generators = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public AIProviderCatalog(AIOptions options, IEnumerable<IChatClientFactory> chatFactories, IEnumerable<IEmbeddingGeneratorFactory> embeddingFactories)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(chatFactories);
        ArgumentNullException.ThrowIfNull(embeddingFactories);
        _options = options;
        _chatFactories = new Dictionary<string, IChatClientFactory>(StringComparer.Ordinal);
        foreach (var factory in chatFactories)
        {
            _chatFactories[factory.Kind.ToLowerInvariant()] = factory;
        }

        _embeddingFactories = new Dictionary<string, IEmbeddingGeneratorFactory>(StringComparer.Ordinal);
        foreach (var factory in embeddingFactories)
        {
            _embeddingFactories[factory.Kind.ToLowerInvariant()] = factory;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Providers => _options.Providers.Keys.ToList();

    /// <inheritdoc />
    public Maybe<AIProviderOptions> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _options.Providers.TryGetValue(name, out var options) ? Maybe.Some(options) : Maybe.None<AIProviderOptions>();
    }

    /// <inheritdoc />
    public Result<IChatClient> ResolveChatClient(string? name = null)
    {
        var resolved = name ?? _options.ResolveDefaultProvider();
        if (resolved is null || !_options.Providers.TryGetValue(resolved, out var options))
        {
            return NotFound(resolved);
        }

        lock (_lock)
        {
            if (_chatClients.TryGetValue(resolved, out var cached))
            {
                return Result.Success(cached);
            }

            if (!_chatFactories.TryGetValue(options.Kind, out var factory))
            {
                return KindUnknown(options);
            }

            // 创建在锁内：同名并发解析只建一个实例
            var created = factory.Create(options);
            if (created.TryGetValue(out var client))
            {
                _chatClients[resolved] = client;
            }

            return created;
        }
    }

    /// <inheritdoc />
    public Result<IEmbeddingGenerator<string, Embedding<float>>> ResolveEmbeddingGenerator(string? name = null)
    {
        var resolved = name ?? _options.ResolveDefaultEmbeddingProvider();
        if (resolved is null || !_options.Providers.TryGetValue(resolved, out var options))
        {
            return NotFound(resolved);
        }

        lock (_lock)
        {
            if (_generators.TryGetValue(resolved, out var cached))
            {
                return Result.Success(cached);
            }

            if (!_embeddingFactories.TryGetValue(options.Kind, out var factory))
            {
                return KindUnknown(options);
            }

            var created = factory.Create(options);
            if (created.TryGetValue(out var generator))
            {
                _generators[resolved] = generator;
            }

            return created;
        }
    }

    /// <inheritdoc />
    public void Invalidate(string? name = null)
    {
        List<IDisposable> disposables = [];
        lock (_lock)
        {
            if (name is null)
            {
                disposables.AddRange(_chatClients.Values.OfType<IDisposable>());
                disposables.AddRange(_generators.Values.OfType<IDisposable>());
                _chatClients.Clear();
                _generators.Clear();
            }
            else
            {
                if (_chatClients.Remove(name, out var client) && client is IDisposable disposableClient)
                {
                    disposables.Add(disposableClient);
                }

                if (_generators.Remove(name, out var generator) && generator is IDisposable disposableGenerator)
                {
                    disposables.Add(disposableGenerator);
                }
            }
        }

        // 释放放在锁外：释放可能阻塞或抛出，不拖住其他解析
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Invalidate();

    /// <summary>
    /// Provider 未配置。
    /// </summary>
    private static Error NotFound(string? name)
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.ProviderNotFound, MessageArguments.Of(("name", name ?? string.Empty)));

    /// <summary>
    /// 种类无工厂。
    /// </summary>
    private static Error KindUnknown(AIProviderOptions options)
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.ProviderKindUnknown, MessageArguments.Of(("name", options.Name), ("kind", options.Kind)));
}
