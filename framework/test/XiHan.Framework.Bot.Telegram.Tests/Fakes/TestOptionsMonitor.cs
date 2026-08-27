// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// 可手动推送变更的选项监视器替身
/// </summary>
/// <remarks>
/// 平台内几乎所有组件都通过 <see cref="IOptionsMonitor{TOptions}"/> 读取文案与重试配置，
/// 真实 OptionsMonitor 需要变更令牌源才能触发回调，这里直接暴露 <see cref="Set"/> 供用例确定性地改值。
/// </remarks>
/// <typeparam name="TOptions">选项类型</typeparam>
internal sealed class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class
{
    private readonly List<Action<TOptions, string?>> _listeners = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">初始选项值</param>
    public TestOptionsMonitor(TOptions value)
    {
        CurrentValue = value;
    }

    /// <summary>
    /// 当前选项值
    /// </summary>
    public TOptions CurrentValue { get; private set; }

    /// <summary>
    /// 按名称获取选项值（本替身忽略名称）
    /// </summary>
    /// <param name="name">选项名称</param>
    /// <returns>选项值</returns>
    public TOptions Get(string? name)
    {
        return CurrentValue;
    }

    /// <summary>
    /// 注册变更回调
    /// </summary>
    /// <param name="listener">回调</param>
    /// <returns>取消注册句柄</returns>
    public IDisposable? OnChange(Action<TOptions, string?> listener)
    {
        _listeners.Add(listener);
        return new Registration(() => _listeners.Remove(listener));
    }

    /// <summary>
    /// 替换选项值并通知全部订阅者
    /// </summary>
    /// <param name="value">新的选项值</param>
    public void Set(TOptions value)
    {
        CurrentValue = value;
        foreach (var listener in _listeners.ToArray())
        {
            listener(value, null);
        }
    }

    /// <summary>
    /// 回调注册句柄
    /// </summary>
    private sealed class Registration : IDisposable
    {
        private readonly Action _onDispose;

        public Registration(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
