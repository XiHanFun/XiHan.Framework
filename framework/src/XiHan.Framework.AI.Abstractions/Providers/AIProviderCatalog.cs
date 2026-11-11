// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// Provider 目录的实现（单例）
/// </summary>
/// <remarks>
/// 按名字找配置，按种类找工厂，实例按名字缓存；同一种类登记多个工厂以最后登记的为准。默认名从配置给出（<see cref="AIOptions.ResolveDefaultProvider"/>、<see cref="AIOptions.ResolveDefaultEmbeddingProvider"/>）
/// </remarks>
internal sealed class AIProviderCatalog : IAIProviderCatalog, IDisposable
{
    /// <summary>
    /// 保护缓存的锁
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 选项
    /// </summary>
    private readonly AIOptions _options;

    /// <summary>
    /// 种类 → 聊天工厂
    /// </summary>
    private readonly Dictionary<string, IChatClientFactory> _chatFactories;

    /// <summary>
    /// 种类 → 嵌入工厂
    /// </summary>
    private readonly Dictionary<string, IEmbeddingGeneratorFactory> _embeddingFactories;

    /// <summary>
    /// 名字 → 聊天客户端
    /// </summary>
    private readonly Dictionary<string, IChatClient> _chatClients = new(StringComparer.Ordinal);

    /// <summary>
    /// 名字 → 嵌入生成器
    /// </summary>
    private readonly Dictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _generators = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建
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
    /// <remarks>
    /// 配置的 <c>DefaultProvider</c>；没写且只配了一个 Provider 时是它
    /// </remarks>
    public string? DefaultProvider => _options.ResolveDefaultProvider();

    /// <inheritdoc />
    /// <remarks>
    /// 配置的 <c>DefaultEmbeddingProvider</c>；没写时同 <see cref="DefaultProvider"/>
    /// </remarks>
    public string? DefaultEmbeddingProvider => _options.ResolveDefaultEmbeddingProvider();

    /// <inheritdoc />
    public Maybe<AIProviderOptions> Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _options.Providers.TryGetValue(name, out var options) ? Maybe.Some(options) : Maybe.None<AIProviderOptions>();
    }

    /// <inheritdoc />
    public Result<IChatClient> ResolveChatClient(string? name = null)
    {
        var resolved = name ?? DefaultProvider;
        if (resolved is null)
        {
            return DefaultMissing();
        }

        if (!_options.Providers.TryGetValue(resolved, out var options))
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
        var resolved = name ?? DefaultEmbeddingProvider;
        if (resolved is null)
        {
            return DefaultEmbeddingMissing();
        }

        if (!_options.Providers.TryGetValue(resolved, out var options))
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
    public void Invalidate(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        List<IDisposable> disposables = [];
        lock (_lock)
        {
            foreach (var name in names)
            {
                ArgumentNullException.ThrowIfNull(name, nameof(names));
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

        Release(disposables);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        List<IDisposable> disposables = [];
        lock (_lock)
        {
            disposables.AddRange(_chatClients.Values.OfType<IDisposable>());
            disposables.AddRange(_generators.Values.OfType<IDisposable>());
            _chatClients.Clear();
            _generators.Clear();
        }

        Release(disposables);
    }

    /// <inheritdoc />
    public void Dispose() => InvalidateAll();

    /// <summary>
    /// 释放丢弃的实例
    /// </summary>
    /// <remarks>
    /// 放在锁外调用：释放可能阻塞或抛出，不拖住其他解析
    /// </remarks>
    private static void Release(List<IDisposable> disposables)
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Provider 未配置
    /// </summary>
    internal static Error NotFound(string name)
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.ProviderNotFound, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 没给名字而没有默认聊天 Provider
    /// </summary>
    internal static Error DefaultMissing()
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.DefaultProviderMissing);

    /// <summary>
    /// 没给名字而没有默认嵌入 Provider
    /// </summary>
    internal static Error DefaultEmbeddingMissing()
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.DefaultEmbeddingProviderMissing);

    /// <summary>
    /// 种类无工厂
    /// </summary>
    private static Error KindUnknown(AIProviderOptions options)
        => Error.NotFound(AIErrors.ProviderNotFound, AIMessages.ProviderKindUnknown, MessageArguments.Of(("name", options.Name), ("kind", options.Kind)));
}
