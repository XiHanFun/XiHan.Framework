#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApiUsageRule
// Guid:1d4e7a2b-5c8f-4a9d-8e1f-3b6c9d2e5f8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/14 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
}
