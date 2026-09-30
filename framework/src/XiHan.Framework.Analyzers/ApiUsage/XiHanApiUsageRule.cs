// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Analyzers.ApiUsage;

/// <summary>
/// 曦寒 API 使用规则描述符集合
/// </summary>
internal static class XiHanApiUsageRule
{
    internal const string Category = "XiHan.ApiUsage";

    /// <summary>
    /// XHFA001：禁止直接 new HttpClient()，应通过 IHttpClientFactory / 框架 IHttpClientService 获取，
    /// 以复用连接池、走工厂管道（重试/熔断）并避免 socket 耗尽。
    /// </summary>
    internal const string DirectHttpClientCreationId = "XHFA001";

    internal static readonly DiagnosticDescriptor DirectHttpClientCreation = new(
        DirectHttpClientCreationId,
        "避免直接 new HttpClient",
        "直接 new HttpClient 会绕过连接池与工厂管道，请改用 IHttpClientFactory 或框架 IHttpClientService",
        Category,
        // Info 级：作为开发期指引出现，不打断构建（框架 HTTP 模块内部有意直建的场景可局部 #pragma 抑制）
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "直接 new HttpClient() 易造成 socket 句柄耗尽，且不经过工厂管道（重试、熔断、代理策略），推荐通过依赖注入的 IHttpClientFactory 或框架的 IHttpClientService 获取实例.");

    /// <summary>
    /// XHFA002：对外可见的异步方法已有取消令牌参数，调用带可选取消令牌参数的方法时省略了该参数
    /// </summary>
    internal const string CancellationTokenNotForwardedId = "XHFA002";

    internal static readonly DiagnosticDescriptor CancellationTokenNotForwarded = new(
        CancellationTokenNotForwardedId,
        "转发取消令牌",
        "调用 {0} 时省略了取消令牌参数，请转发 {1}",
        Category,
        // 默认 Info；仓库内由 .editorconfig 提升为 warning
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "对外可见的异步方法接收了取消令牌，调用带可选取消令牌参数的方法时应把它传下去；确需不可取消时显式传入 CancellationToken.None.");
}
