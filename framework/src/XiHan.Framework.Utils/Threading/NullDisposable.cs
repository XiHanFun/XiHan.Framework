// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 空可释放对象
/// </summary>
public sealed class NullDisposable : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullDisposable()
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullDisposable Instance { get; } = new();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
    }
}
