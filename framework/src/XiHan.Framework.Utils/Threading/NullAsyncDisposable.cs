// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 空可异步释放对象
/// </summary>
public sealed class NullAsyncDisposable : IAsyncDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullAsyncDisposable()
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullAsyncDisposable Instance { get; } = new();

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        return default;
    }
}
