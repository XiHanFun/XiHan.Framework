// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 冒充插件的 <c>cache-entries.set</c>：一律以过期选项不合法拒绝
/// </summary>
/// <remarks>
/// 模拟插件按操作集（<c>spec/provider/caching-distributed.md</c>）校验 <c>ttlMs</c> / <c>slidingMs</c> 后返回 <c>CACHING_VALIDATION_002</c>
/// </remarks>
internal sealed class RejectingSetOperation : IOperation<RemoteCacheOperations.SetRequest, RemoteCacheOperations.EmptyResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<RemoteCacheOperations.EmptyResponse>> HandleAsync(RemoteCacheOperations.SetRequest request, InvocationContext context)
        => ValueTask.FromResult<Result<RemoteCacheOperations.EmptyResponse>>(Error.Validation(CachingErrors.InvalidOptions, RemoteCachingMessages.InvalidOptions));
}
