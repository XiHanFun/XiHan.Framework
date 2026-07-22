// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化接口
/// </summary>
public interface IOnApplicationInitialization
{
    /// <summary>
    /// 程序初始化，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化
    /// </summary>
    /// <param name="context"></param>
    void OnApplicationInitialization(ApplicationInitializationContext context);
}
