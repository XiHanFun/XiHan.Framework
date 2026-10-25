// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// Provider 目录：按名字解析聊天客户端与嵌入生成器，解析结果缓存到 <see cref="Invalidate"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIProviderCatalog
{
    /// <summary>
    /// 已配置的 Provider 名。
    /// </summary>
    IReadOnlyCollection<string> Providers { get; }

    /// <summary>
    /// 查 Provider 配置。
    /// </summary>
    Maybe<AIProviderOptions> Find(string name);

    /// <summary>
    /// 解析聊天客户端；<paramref name="name"/> 为空取默认 Provider。未配置或种类无工厂 → <see cref="AIErrors.ProviderNotFound"/>。
    /// </summary>
    Result<IChatClient> ResolveChatClient(string? name = null);

    /// <summary>
    /// 解析嵌入生成器；<paramref name="name"/> 为空取默认嵌入 Provider。
    /// </summary>
    Result<IEmbeddingGenerator<string, Embedding<float>>> ResolveEmbeddingGenerator(string? name = null);

    /// <summary>
    /// 丢弃缓存的实例（可释放的会被释放）；<paramref name="name"/> 为空清全部。
    /// </summary>
    void Invalidate(string? name = null);
}
