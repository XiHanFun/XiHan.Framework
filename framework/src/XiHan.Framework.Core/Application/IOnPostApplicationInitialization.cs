// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化后接口
/// </summary>
public interface IOnPostApplicationInitialization
{
    /// <summary>
    /// 程序初始化后，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化后
    /// </summary>
    /// <param name="context"></param>
    void OnPostApplicationInitialization(ApplicationInitializationContext context);
}
