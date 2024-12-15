#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncDisposeFunc
// Guid:058ba260-69a1-44f2-985f-27b0ee1859ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 3:39:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 异步释放函数
/// </summary>
public class AsyncDisposeFunc : IAsyncDisposable
{
    private readonly Func<Task> _func;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="func">此对象在执行 DisposeAsync 时要执行的函数</param>
    public AsyncDisposeFunc(Func<Task> func)
    {
        _ = CheckHelper.NotNull(func, nameof(func));

        _func = func;
    }

    /// <summary>
    /// 释放
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await _func();
    }
}
