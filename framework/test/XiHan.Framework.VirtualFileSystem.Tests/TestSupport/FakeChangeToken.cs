// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.VirtualFileSystem.Tests.TestSupport;

/// <summary>
/// 手写的变更令牌替身
/// </summary>
internal sealed class FakeChangeToken : IChangeToken
{
    /// <summary>
    /// 是否已发生变化
    /// </summary>
    public bool HasChanged { get; set; }

    /// <summary>
    /// 是否支持主动回调
    /// </summary>
    public bool ActiveChangeCallbacks { get; set; }

    /// <summary>
    /// 回调注册次数
    /// </summary>
    public int RegisterCallbackCount { get; private set; }

    /// <summary>
    /// 注册变化回调
    /// </summary>
    /// <param name="callback">回调</param>
    /// <param name="state">状态</param>
    /// <returns>注销句柄</returns>
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        RegisterCallbackCount++;
        return new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
