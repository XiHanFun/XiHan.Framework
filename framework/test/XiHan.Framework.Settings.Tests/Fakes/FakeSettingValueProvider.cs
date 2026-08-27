// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 可编程的设置值提供者替身
/// </summary>
/// <remarks>
/// 返回固定值并统计被调用次数，用于验证提供者链的覆盖顺序与短路行为。
/// </remarks>
public sealed class FakeSettingValueProvider : ISettingValueProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="value">该提供者返回的固定值，null 表示"未命中"</param>
    public FakeSettingValueProvider(string name, string? value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 固定返回值
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 单项读取被调用的次数
    /// </summary>
    public int GetOrNullCallCount { get; private set; }

    /// <summary>
    /// 批量读取被调用的次数
    /// </summary>
    public int GetAllCallCount { get; private set; }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting">设置定义</param>
    /// <returns>固定返回值</returns>
    public Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        GetOrNullCallCount++;
        return Task.FromResult(Value);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings">设置定义数组</param>
    /// <returns>设置值列表</returns>
    public Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        GetAllCallCount++;
        var result = settings.Select(x => new SettingValue(x.Name, Value)).ToList();
        return Task.FromResult(result);
    }
}
