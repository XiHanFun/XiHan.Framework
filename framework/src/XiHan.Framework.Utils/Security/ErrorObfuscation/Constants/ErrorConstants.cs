#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorConstants
// Guid:1e2f3a4b-6c7d-8e9f-0a1b-2c3d4e5f6a7b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Constants;

/// <summary>
/// 错误混淆常量
/// </summary>
internal static class ErrorConstants
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public static readonly string[] ErrorTypes =
    [
        "InternalServerError",
        "DatabaseConnectionError",
        "ServiceUnavailable",
        "BadGateway",
        "GatewayTimeout",
        "UnauthorizedAccess",
        "ForbiddenResource",
        "ConfigurationError",
        "MemoryAllocationError",
        "NetworkError",
        "TimeoutError",
        "ValidationError",
        "AuthenticationError",
        "RateLimitExceeded",
        "ResourceNotFound",
        "MethodNotAllowed",
        "ConflictError",
        "PayloadTooLarge",
        "UnprocessableEntity",
        "InsufficientStorage"
    ];

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public static readonly int[] HttpStatusCodes =
    [
        400, 401, 403, 404, 405, 408, 409, 413, 422, 429,
        500, 502, 503, 504, 507, 511
    ];

    /// <summary>
    /// 服务器名称
    /// </summary>
    public static readonly string[] ServerNames =
    [
        "Apache/2.4.54 (Ubuntu)",
        "nginx/1.22.1",
        "Microsoft-IIS/10.0",
        "LiteSpeed/5.4.12",
        "Tomcat/9.0.71",
        "Jetty/11.0.13",
        "Kestrel/7.0.4",
        "gunicorn/20.1.0",
        "Uvicorn/0.20.0",
        "Passenger/6.0.16",
        "WEBrick/1.8.1",
        "Puma/6.0.2",
        "Node.js/v18.14.2",
        "Express/4.18.2"
    ];

    /// <summary>
    /// 请求路径
    /// </summary>
    public static readonly string[] RequestPaths =
    [
        "/api/v1/users",
        "/api/v2/products",
        "/api/v1/orders",
        "/api/v2/payments",
        "/admin/dashboard",
        "/admin/users",
        "/account/login",
        "/account/register",
        "/data/export",
        "/data/import",
        "/services/process",
        "/services/validate",
        "/resources/load",
        "/resources/images",
        "/config/settings",
        "/health/check",
        "/metrics/stats",
        "/graphql/query",
        "/webhook/callback",
        "/upload/file"
    ];

    /// <summary>
    /// 数据库名称
    /// </summary>
    public static readonly string[] DatabaseNames =
    [
        "MySQL 8.0.32",
        "PostgreSQL 15.2",
        "MongoDB 6.0.4",
        "Redis 7.0.8",
        "SQL Server 2022",
        "Oracle 19c",
        "MariaDB 10.11.2",
        "Cassandra 4.1.0",
        "SQLite 3.40.1",
        "Elasticsearch 8.6.1"
    ];

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public static readonly string[] HttpMethods =
    [
        "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD"
    ];

    /// <summary>
    /// 主机名前缀
    /// </summary>
    public static readonly string[] HostnamePrefixes =
    [
        "web", "app", "api", "server", "node", "host", "prod", "srv"
    ];

    /// <summary>
    /// 主机名域
    /// </summary>
    public static readonly string[] HostnameDomains =
    [
        "internal", "local", "cloud", "cluster", "prod", "backend"
    ];
}
