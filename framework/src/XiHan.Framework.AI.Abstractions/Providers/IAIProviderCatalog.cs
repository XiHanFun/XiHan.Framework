// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// Provider 目录：按名字解析聊天客户端与嵌入生成器，解析结果缓存到 <see cref="Invalidate"/>
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIProviderCatalog
{
    /// <summary>
    /// 已配置的 Provider 名
    /// </summary>
    IReadOnlyCollection<string> Providers { get; }

    /// <summary>
    /// 默认聊天 Provider 名
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示没有默认。<see cref="ResolveChatClient"/> 不给名字时按它解析，<c>IAIChat</c> 在请求没写 Provider 时也只问它、不读配置
    /// 读它不做 I/O：要查库的目录持内存快照、写入后重载，默认名是快照的一部分，所以是同步属性、不带取消令牌。名字不保证在 <see cref="Providers"/> 里，按它解析时照常报未配置
    /// </remarks>
    string? DefaultProvider { get; }

    /// <summary>
    /// 默认嵌入 Provider 名
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示没有默认。<see cref="ResolveEmbeddingGenerator"/> 不给名字时按它解析，知识库的摄入与检索在请求与 <c>XiHan:AI:Knowledge:EmbeddingProvider</c> 都没写时也只问它
    /// 可以与 <see cref="DefaultProvider"/> 相同；只支持一个默认的目录两者相同
    /// </remarks>
    string? DefaultEmbeddingProvider { get; }

    /// <summary>
    /// 查 Provider 配置
    /// </summary>
    Maybe<AIProviderOptions> Find(string name);

    /// <summary>
    /// 解析聊天客户端
    /// </summary>
    /// <remarks>
    /// <paramref name="name"/> 为空取 <see cref="DefaultProvider"/>，没有默认 → <see cref="AIErrors.ProviderNotFound"/>（<see cref="AIMessages.DefaultProviderMissing"/>）。未配置或种类无工厂 → <see cref="AIErrors.ProviderNotFound"/>
    /// </remarks>
    Result<IChatClient> ResolveChatClient(string? name = null);

    /// <summary>
    /// 解析嵌入生成器
    /// </summary>
    /// <remarks>
    /// <paramref name="name"/> 为空取 <see cref="DefaultEmbeddingProvider"/>，没有默认 → <see cref="AIErrors.ProviderNotFound"/>（<see cref="AIMessages.DefaultEmbeddingProviderMissing"/>）
    /// </remarks>
    Result<IEmbeddingGenerator<string, Embedding<float>>> ResolveEmbeddingGenerator(string? name = null);

    /// <summary>
    /// 丢弃这些提供方缓存的实例
    /// </summary>
    /// <remarks>
    /// 可释放的会被释放；空集合什么也不做。清全部用 <see cref="InvalidateAll"/>，不以 <see langword="null"/> 表示全部（ADR-0160）
    /// </remarks>
    void Invalidate(IReadOnlyList<string> names);

    /// <summary>
    /// 丢弃全部缓存的实例
    /// </summary>
    /// <remarks>
    /// 可释放的会被释放
    /// </remarks>
    void InvalidateAll();
}
