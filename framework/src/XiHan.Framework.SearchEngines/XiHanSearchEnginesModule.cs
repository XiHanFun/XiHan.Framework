// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.SearchEngines.Abstractions;
using XiHan.Framework.SearchEngines.InMemory;

namespace XiHan.Framework.SearchEngines;

/// <summary>
/// 曦寒框架搜索引擎模块
/// </summary>
/// <remarks>
/// 只注册进程内实现作为兜底。接入具体搜索引擎时引用对应的实现包，
/// 其模块以 <c>Replace</c> 覆盖 <see cref="ISearchEngine"/> 的注册。
/// </remarks>
public class XiHanSearchEnginesModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<InMemorySearchEngine>();
        context.Services.TryAddSingleton<ISearchEngine>(sp => sp.GetRequiredService<InMemorySearchEngine>());
    }
}
