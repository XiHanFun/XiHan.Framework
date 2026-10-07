// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// 防重放 nonce 的认领结果。
/// </summary>
public enum OpenApiNonceAcquisitionResult
{
    /// <summary>本次请求成功认领 nonce。</summary>
    Acquired,

    /// <summary>nonce 已被其他请求认领。</summary>
    AlreadyUsed,

    /// <summary>nonce 存储达到容量上限。</summary>
    CapacityExceeded
}
