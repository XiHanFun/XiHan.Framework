// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Bot.WeCom.Tests.Fakes;

/// <summary>
/// 企业微信出站请求测试宿主
/// </summary>
/// <remarks>
/// <c>StringHttpExtensions.AsHttp()</c> 走的是进程级静态服务定位器，一旦初始化就无法再换实例，
/// 因此这里用静态构造函数一次性把 <see cref="CapturingHttpService"/> 装进去。
/// 所有会触碰该静态状态的测试类必须挂 <see cref="WeComHttpCollection"/>，靠集合串行避免互相污染。
/// </remarks>
internal static class WeComHttpTestHost
{
    static WeComHttpTestHost()
    {
        Http = new CapturingHttpService();

        var services = new ServiceCollection();
        services.AddSingleton<IAdvancedHttpService>(Http);
        StringHttpExtensions.Initialize(services.BuildServiceProvider());
    }

    /// <summary>
    /// 进程内唯一的 HTTP 替身
    /// </summary>
    public static CapturingHttpService Http { get; }
}
