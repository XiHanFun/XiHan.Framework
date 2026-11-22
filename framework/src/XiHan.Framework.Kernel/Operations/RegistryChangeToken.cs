// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 登记表一代内容的变更令牌
/// </summary>
/// <remarks>
/// 只触发一次：<see cref="Fire"/> 之后 <see cref="HasChanged"/> 为真，登记的回调按登记顺序同步执行；触发之后再登记的回调立即执行，与 <see cref="CancellationChangeToken"/> 的行为一致。不持有可释放的资源，登记表不用实现 <see cref="IDisposable"/>
/// </remarks>
internal sealed class RegistryChangeToken : IChangeToken
{
    /// <summary>
    /// 回调表的互斥锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 还没执行的回调
    /// </summary>
    /// <remarks>
    /// 触发之后为 <see langword="null"/>；只在 <see cref="_gate"/> 内读写
    /// </remarks>
    private List<Registration>? _registrations = [];

    /// <summary>
    /// 是否已触发
    /// </summary>
    private volatile bool _changed;

    /// <inheritdoc />
    public bool HasChanged => _changed;

    /// <inheritdoc />
    public bool ActiveChangeCallbacks => true;

    /// <inheritdoc />
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var registration = new Registration(this, callback, state);
        lock (_gate)
        {
            if (_registrations is not null)
            {
                _registrations.Add(registration);
                return registration;
            }
        }

        // 已经触发过：立即执行，调用方不会因为登记晚了一步而错过这次变化
        callback(state);
        return registration;
    }

    /// <summary>
    /// 触发令牌
    /// </summary>
    /// <remarks>
    /// 调用方不能持有登记表的锁：回调同步执行，可能回头读登记表或再登记。重复触发什么都不做。每个回调都执行，抛出的异常在全部执行完之后合成 <see cref="AggregateException"/> 抛出，与 <see cref="CancellationTokenSource.Cancel()"/> 一致
    /// </remarks>
    public void Fire()
    {
        List<Registration> registrations;
        lock (_gate)
        {
            if (_registrations is null)
            {
                return;
            }

            // 先在锁内摘下回调表并标记已触发：之后的登记直接执行，不会漏掉、也不会执行两次
            registrations = _registrations;
            _registrations = null;
            _changed = true;
        }

        List<Exception>? failures = null;
        foreach (var registration in registrations)
        {
            try
            {
                registration.Invoke();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }

    /// <summary>
    /// 从回调表里摘掉一个回调
    /// </summary>
    private void Remove(Registration registration)
    {
        lock (_gate)
        {
            _registrations?.Remove(registration);
        }
    }

    /// <summary>
    /// 一个登记的回调
    /// </summary>
    /// <remarks>
    /// 释放即注销：触发之前释放的不再执行
    /// </remarks>
    /// <param name="owner">所属的令牌</param>
    /// <param name="callback">回调</param>
    /// <param name="state">回调的参数</param>
    private sealed class Registration(RegistryChangeToken owner, Action<object?> callback, object? state) : IDisposable
    {
        /// <summary>
        /// 是否已注销
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// 执行回调
        /// </summary>
        /// <remarks>
        /// 已注销的不执行
        /// </remarks>
        public void Invoke()
        {
            if (!_disposed)
            {
                callback(state);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
            owner.Remove(this);
        }
    }
}
