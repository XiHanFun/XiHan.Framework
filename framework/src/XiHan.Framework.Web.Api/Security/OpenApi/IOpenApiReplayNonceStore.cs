// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 请求防重放 nonce 存储。
/// </summary>
/// <remarks>
/// 实现必须原子地检查并认领 (accessKey, nonce)。多实例部署时需由应用注册共享实现，
/// 且必须提供跨实例原子语义；普通分布式缓存的 Get 后 Set 不满足此契约。
/// </remarks>
public interface IOpenApiReplayNonceStore
{
    /// <summary>
    /// 尝试原子认领 nonce。
    /// </summary>
    /// <param name="accessKey">客户端访问键。</param>
    /// <param name="nonce">请求随机串。</param>
    /// <param name="expiresAt">nonce 过期时间（UTC）。</param>
    /// <param name="maxEntries">存储允许保留的最大 nonce 数量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>认领结果。</returns>
    Task<OpenApiNonceAcquisitionResult> TryAcquireAsync(
        string accessKey,
        string nonce,
        DateTimeOffset expiresAt,
        int maxEntries,
        CancellationToken cancellationToken = default);
}
