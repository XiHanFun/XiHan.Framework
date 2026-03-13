#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOpenApiSecurityOptions
// Guid:a59740a5-765d-48cc-98ed-f0fb25dbb3f5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:31:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 安全配置
/// </summary>
public class XiHanOpenApiSecurityOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Api:OpenApiSecurity";

    /// <summary>
    /// 是否启用 OpenApi 安全中间件
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 允许未携带安全头的请求直接通过（灰度开关）
    /// </summary>
    public bool AllowUnsignedRequests { get; set; } = true;

    /// <summary>
    /// 是否必须校验内容签名
    /// </summary>
    public bool RequireContentSignature { get; set; } = true;

    /// <summary>
    /// 是否启用防重放校验
    /// </summary>
    public bool EnableReplayProtection { get; set; } = true;

    /// <summary>
    /// 时间戳允许误差（秒）
    /// </summary>
    public int TimestampToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// Nonce 存活时间（秒）
    /// </summary>
    public int NonceExpireSeconds { get; set; } = 300;

    /// <summary>
    /// 允许读取的最大请求体字节数
    /// </summary>
    public int MaxRequestBodySize { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// 是否启用响应加密
    /// </summary>
    public bool EnableResponseEncryption { get; set; } = true;

    /// <summary>
    /// 当请求已加密时是否默认加密响应
    /// </summary>
    public bool EncryptResponseByDefaultWhenRequestEncrypted { get; set; } = false;

    /// <summary>
    /// 默认签名算法
    /// </summary>
    public string DefaultSignatureAlgorithm { get; set; } = "HMACSHA256";

    /// <summary>
    /// 默认内容签名算法
    /// </summary>
    public string DefaultContentSignatureAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// 默认加密算法
    /// </summary>
    public string DefaultEncryptionAlgorithm { get; set; } = "AES-CBC";

    /// <summary>
    /// 受保护路径前缀列表（为空时表示全路径生效）
    /// </summary>
    public List<string> ProtectedPathPrefixes { get; set; } = ["/api"];

    /// <summary>
    /// 忽略路径前缀列表
    /// </summary>
    public List<string> IgnoredPathPrefixes { get; set; } =
    [
        "/openapi",
        "/swagger",
        "/health"
    ];

    /// <summary>
    /// 配置内客户端（默认客户端存储可直接使用）
    /// </summary>
    public List<OpenApiSecurityClientOptions> Clients { get; set; } = [];
}
