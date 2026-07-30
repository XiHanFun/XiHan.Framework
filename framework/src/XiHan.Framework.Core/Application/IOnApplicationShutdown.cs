// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序关闭时接口
/// </summary>
public interface IOnApplicationShutdown
{
    /// <summary>
    /// 程序关闭时，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnApplicationShutdownAsync(ApplicationShutdownContext context);

    /// <summary>
    /// 程序关闭时
    /// </summary>
    /// <param name="context"></param>
    void OnApplicationShutdown(ApplicationShutdownContext context);
}
