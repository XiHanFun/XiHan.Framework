#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OpenApiSecurityConstants
// Guid:6f9f3a20-2e52-4d9d-9424-ef27bb51ca8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 安全头与上下文常量
/// </summary>
public static class OpenApiSecurityConstants
{
    /// <summary>
    /// AccessKey 请求头
    /// </summary>
    public const string AccessKeyHeaderName = "X-Access-Key";

    /// <summary>
    /// 时间戳请求头（Unix 秒）
    /// </summary>
    public const string TimestampHeaderName = "X-Timestamp";

    /// <summary>
    /// 随机串请求头
    /// </summary>
    public const string NonceHeaderName = "X-Nonce";

    /// <summary>
    /// 请求签名请求头
    /// </summary>
    public const string SignatureHeaderName = "X-Signature";

    /// <summary>
    /// 内容签名请求头
    /// </summary>
    public const string ContentSignHeaderName = "X-Content-Sign";

    /// <summary>
    /// 签名算法请求头
    /// </summary>
    public const string SignatureAlgorithmHeaderName = "X-Sign-Algorithm";

    /// <summary>
    /// 内容签名算法请求头
    /// </summary>
    public const string ContentSignAlgorithmHeaderName = "X-Content-Sign-Algorithm";

    /// <summary>
    /// 请求体加密算法请求头
    /// </summary>
    public const string EncryptAlgorithmHeaderName = "X-Encrypt-Algorithm";

    /// <summary>
    /// 请求体加密 IV 请求头（Base64）
    /// </summary>
    public const string EncryptIvHeaderName = "X-Encrypt-Iv";

    /// <summary>
    /// 是否要求响应加密请求头
    /// </summary>
    public const string EncryptResponseHeaderName = "X-Encrypt-Response";

    /// <summary>
    /// 响应已加密响应头
    /// </summary>
    public const string SecureResponseHeaderName = "X-Secure-Response";

    /// <summary>
    /// 上下文中 OpenApi 客户端信息键
    /// </summary>
    public const string SecurityClientContextKey = "__XiHanOpenApiClient";
}
