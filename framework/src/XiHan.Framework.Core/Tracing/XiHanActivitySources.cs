#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanActivitySources
// Guid:7f335df8-66eb-4213-85d0-62224291d2f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/09 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;

namespace XiHan.Framework.Core.Tracing;

/// <summary>
/// 曦寒共享 ActivitySource 名与实例
/// </summary>
/// <remarks>
/// 放在 Core.Tracing 而非 Observability：Data/EventBus/Http/Web 都引用 Core 却不能反向依赖 Observability，
/// 源常量与实例集中在此，各层才能发 Span 而不产生环依赖。
/// Observability 的 OTel 装配用 <see cref="All"/> 一次性 AddSource；各层 new Activity 用对应静态源。
/// </remarks>
public static class XiHanActivitySources
{
    /// <summary>应用/业务层自定义 Span 源</summary>
    public const string App = "XiHan.App";

    /// <summary>数据库（SqlSugar）Span 源</summary>
    public const string Data = "XiHan.Data";

    /// <summary>事件总线（EventBus）Span 源</summary>
    public const string EventBus = "XiHan.EventBus";

    /// <summary>gRPC Span 源</summary>
    public const string Grpc = "XiHan.Grpc";

    /// <summary>缓存（Redis）Span 源</summary>
    public const string Cache = "XiHan.Cache";

    /// <summary>AI 会话管道 Span 源（须与 AiPipelineOptions.TelemetrySourceName 一致）</summary>
    public const string Ai = "XiHan.AI";

    /// <summary>应用/业务层 Span 源实例</summary>
    public static readonly ActivitySource AppSource = new(App);

    /// <summary>数据库 Span 源实例</summary>
    public static readonly ActivitySource DataSource = new(Data);

    /// <summary>事件总线 Span 源实例</summary>
    public static readonly ActivitySource EventBusSource = new(EventBus);

    /// <summary>gRPC Span 源实例</summary>
    public static readonly ActivitySource GrpcSource = new(Grpc);

    /// <summary>缓存（Redis）Span 源实例</summary>
    public static readonly ActivitySource CacheSource = new(Cache);

    /// <summary>框架内置的全部源名（供 OTel AddSource 批量注册）</summary>
    public static readonly string[] All = [App, Data, EventBus, Grpc, Cache, Ai];
}
