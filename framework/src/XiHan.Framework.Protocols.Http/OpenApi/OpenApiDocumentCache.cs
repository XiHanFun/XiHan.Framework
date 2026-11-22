// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 按组与区域缓存的 OpenAPI 文档
/// </summary>
/// <remarks>
/// 登记表或任一分组来源的令牌触发时整体换新（ADR-0251）：正在生成的那份写进旧缓存、随旧缓存丢掉，下一次请求按新内容重新生成。值为 <see langword="null"/> 表示这一组此刻不存在
/// </remarks>
internal sealed class OpenApiDocumentCache
{
    /// <summary>
    /// 当前这一代的缓存
    /// </summary>
    /// <remarks>
    /// 换新时整体替换，读写都经 <see cref="Volatile"/>
    /// </remarks>
    private ConcurrentDictionary<(string? Group, string Locale), Lazy<byte[]?>> _documents = new();

    /// <summary>
    /// 取一份文档，没有就生成
    /// </summary>
    /// <param name="key">组名（全量文档为 <see langword="null"/>）与区域（没有装配本地化时为空串）</param>
    /// <param name="generate">生成文档；这一组不存在时返回 <see langword="null"/></param>
    public byte[]? GetOrAdd((string? Group, string Locale) key, Func<(string? Group, string Locale), byte[]?> generate)
        => Volatile.Read(ref _documents).GetOrAdd(key, static (key, generate) => new Lazy<byte[]?>(() => generate(key)), generate).Value;

    /// <summary>
    /// 丢掉全部缓存
    /// </summary>
    public void Clear() => Volatile.Write(ref _documents, new());

    /// <summary>
    /// 文档输入变化的令牌
    /// </summary>
    /// <remarks>
    /// 登记表与各个分组来源的令牌任一触发即触发
    /// </remarks>
    /// <param name="registry">操作登记表</param>
    /// <param name="sources">分组来源</param>
    public static IChangeToken ChangeOf(OperationRegistry registry, IReadOnlyList<IOpenApiGroupSource> sources)
        => sources.Count == 0 ? registry.GetChangeToken() : new CompositeChangeToken([registry.GetChangeToken(), .. sources.Select(static source => source.GetChangeToken())]);
}
