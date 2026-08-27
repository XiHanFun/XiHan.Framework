// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 记录调用参数的设置存储替身
/// </summary>
/// <remarks>
/// 用内存字典模拟持久化，同时逐条记录每次调用的提供者名与提供者键，
/// 以便断言设置管理器与各值提供者的存储路由是否正确（本仓测试栈不引入 mock 框架，替身一律手写）。
/// </remarks>
public sealed class FakeSettingStore : ISettingStore
{
    /// <summary>
    /// 已持久化的值，键由 <see cref="BuildKey"/> 生成
    /// </summary>
    public Dictionary<string, string?> Values { get; } = [];

    /// <summary>
    /// 单项读取调用记录
    /// </summary>
    public List<SingleCall> GetOrNullCalls { get; } = [];

    /// <summary>
    /// 批量读取调用记录
    /// </summary>
    public List<BatchCall> GetAllCalls { get; } = [];

    /// <summary>
    /// 写入调用记录
    /// </summary>
    public List<SingleCall> SetCalls { get; } = [];

    /// <summary>
    /// 删除调用记录
    /// </summary>
    public List<SingleCall> DeleteCalls { get; } = [];

    /// <summary>
    /// 生成内部存储键
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>内部存储键</returns>
    public static string BuildKey(string name, string? providerName, string? providerKey)
    {
        return $"{name}|{providerName}|{providerKey}";
    }

    /// <summary>
    /// 预置一条已持久化的值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <param name="value">设置值</param>
    public void Seed(string name, string? providerName, string? providerKey, string? value)
    {
        Values[BuildKey(name, providerName, providerKey)] = value;
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值</returns>
    public Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        GetOrNullCalls.Add(new SingleCall(name, null, providerName, providerKey));
        return Task.FromResult(Values.GetValueOrDefault(BuildKey(name, providerName, providerKey)));
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="names">设置名称数组</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值列表</returns>
    public Task<List<SettingValue>> GetAllAsync(string[] names, string? providerName, string? providerKey)
    {
        GetAllCalls.Add(new BatchCall(names, providerName, providerKey));
        var result = names
            .Select(x => new SettingValue(x, Values.GetValueOrDefault(BuildKey(x, providerName, providerKey))))
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>
    /// 写入设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="value">设置值</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>异步任务</returns>
    public Task SetAsync(string name, string? value, string? providerName, string? providerKey)
    {
        SetCalls.Add(new SingleCall(name, value, providerName, providerKey));
        Values[BuildKey(name, providerName, providerKey)] = value;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>异步任务</returns>
    public Task DeleteAsync(string name, string? providerName, string? providerKey)
    {
        DeleteCalls.Add(new SingleCall(name, null, providerName, providerKey));
        Values.Remove(BuildKey(name, providerName, providerKey));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 单项调用记录
    /// </summary>
    /// <param name="Name">设置名称</param>
    /// <param name="Value">设置值（仅写入调用有意义）</param>
    /// <param name="ProviderName">提供者名称</param>
    /// <param name="ProviderKey">提供者键</param>
    public sealed record SingleCall(string Name, string? Value, string? ProviderName, string? ProviderKey);

    /// <summary>
    /// 批量调用记录
    /// </summary>
    /// <param name="Names">设置名称数组</param>
    /// <param name="ProviderName">提供者名称</param>
    /// <param name="ProviderKey">提供者键</param>
    public sealed record BatchCall(string[] Names, string? ProviderName, string? ProviderKey);
}
