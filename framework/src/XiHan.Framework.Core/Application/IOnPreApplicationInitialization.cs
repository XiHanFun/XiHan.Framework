// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化前接口
/// </summary>
public interface IOnPreApplicationInitialization
{
    /// <summary>
    /// 程序初始化前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnPreApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化前
    /// </summary>
    /// <param name="context"></param>
    void OnPreApplicationInitialization(ApplicationInitializationContext context);
}
