// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// <c>caching.distributed@1</c> 远程 Provider 的操作集（<c>spec/provider/caching-distributed.md</c>）：操作 ID 与请求、响应的 JSON 形态。
/// 值与期望值是标准 base64；<c>ttlMs</c> 与 <c>slidingMs</c> 二选一，都不给用插件的默认存活时间。批量操作的响应数组与请求数组等长、顺序一致。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public static class RemoteCacheOperations
{
    /// <summary>读取：不存在或已过期为 <see langword="null"/>，滑动过期在读取时延长。</summary>
    public const string Get = "cache-entries.get";

    /// <summary>写入或覆盖。</summary>
    public const string Set = "cache-entries.set";

    /// <summary>不存在（或已过期）才写入，原子。</summary>
    public const string TryAdd = "cache-entries.try-add";

    /// <summary>删除并报告是否存在。</summary>
    public const string Remove = "cache-entries.remove";

    /// <summary>当前值等于期望值才删除，原子。</summary>
    public const string TryRemove = "cache-entries.try-remove";

    /// <summary>批量读取：逐键同 <see cref="Get"/>，一次往返。</summary>
    public const string GetMany = "cache-entries.get-many";

    /// <summary>批量写入：逐项同 <see cref="Set"/>，一次往返，同一键后者覆盖。</summary>
    public const string SetMany = "cache-entries.set-many";

    /// <summary>批量删除：逐键同 <see cref="Remove"/>，一次往返。</summary>
    public const string RemoveMany = "cache-entries.remove-many";

    /// <summary>
    /// 操作集的全部操作，按规范顺序。
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Get, Set, TryAdd, Remove, TryRemove, GetMany, SetMany, RemoveMany];

    /// <summary>
    /// <c>cache-entries.get</c> 与 <c>cache-entries.remove</c> 的请求。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record KeyRequest(string Key);

    /// <summary>
    /// <c>cache-entries.get</c> 的响应：值的 base64，不存在为 <see langword="null"/>。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record GetResponse(string? Value);

    /// <summary>
    /// <c>cache-entries.set</c> 与 <c>cache-entries.try-add</c> 的请求，也是 <c>cache-entries.set-many</c> 的一项。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record SetRequest(string Key, string Value, long? TtlMs, long? SlidingMs);

    /// <summary>
    /// 没有字段的响应。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record EmptyResponse;

    /// <summary>
    /// <c>cache-entries.try-add</c> 的响应。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record AddedResponse(bool Added);

    /// <summary>
    /// <c>cache-entries.remove</c> 与 <c>cache-entries.try-remove</c> 的响应。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record RemovedResponse(bool Removed);

    /// <summary>
    /// <c>cache-entries.try-remove</c> 的请求。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record TryRemoveRequest(string Key, string Expected);

    /// <summary>
    /// <c>cache-entries.get-many</c> 与 <c>cache-entries.remove-many</c> 的请求。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record KeysRequest(IReadOnlyList<string> Keys);

    /// <summary>
    /// <c>cache-entries.get-many</c> 的响应：与请求的键等长，每项是值的 base64 或 <see langword="null"/>。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record GetManyResponse(IReadOnlyList<string?> Values);

    /// <summary>
    /// <c>cache-entries.set-many</c> 的请求。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record SetManyRequest(IReadOnlyList<SetRequest> Entries);

    /// <summary>
    /// <c>cache-entries.remove-many</c> 的响应：与请求的键等长的布尔数组。
    /// </summary>
    [ApiLevel(Stability.Experimental, "5.0")]
    public sealed record RemovedManyResponse(IReadOnlyList<bool> Removed);
}
