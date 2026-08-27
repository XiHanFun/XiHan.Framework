// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Tests.Fakes;

/// <summary>
/// 最小日志构建器替身
/// </summary>
/// <remarks>
/// AddXiHanFileLogger / AddXiHanConsoleLogger 只依赖 ILoggingBuilder.Services 这一个成员。
/// 走 AddLogging 会顺带注册一批框架自带服务，干扰对「本项目到底往容器里放了什么」的断言，
/// 因此这里给一个只暴露服务集合的构建器，让注册结果可以被精确观察。
/// </remarks>
/// <param name="services">服务集合</param>
internal sealed class TestLoggingBuilder(IServiceCollection services) : ILoggingBuilder
{
    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
