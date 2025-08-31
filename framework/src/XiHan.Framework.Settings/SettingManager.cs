#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingManager
// Guid:7b8c9d0a-1e3d-4a9f-9c1d-6d3f0a8b2c1b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024-04-23 上午 11:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Events;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Settings;

/// <summary>
/// 设置管理器
/// </summary>
public class SettingManager : ISettingManager, ISingletonDependency
{
    private readonly ILogger<SettingManager> _logger;
    private readonly ISettingStore _settingStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, SettingDefinition> _definitions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="settingStore"></param>
    /// <param name="serviceProvider"></param>
    public SettingManager(
        ILogger<SettingManager> logger,
        ISettingStore settingStore,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _settingStore = settingStore;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 设置值变更事件
    /// </summary>
    public event EventHandler<SettingChangedEventArgs>? OnSettingChanged;

    /// <summary>
    /// 添加设置定义
    /// </summary>
    /// <param name="definition"></param>
    /// <exception cref="XiHanException"></exception>
    public void AddDefinition(SettingDefinition definition)
    {
        if (!_definitions.TryAdd(definition.Name, definition))
        {
            throw new XiHanException($"设置 '{definition.Name}' 已经存在");
        }
    }

    /// <summary>
    /// 获取分组设置
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public IEnumerable<SettingDefinition> GetGroupSettings(string group)
    {
        return _definitions.Values.Where(d => d.Group == group);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="scope"></param>
    /// <returns></returns>
    public async Task<List<SettingValue>> GetAllValuesAsync(SettingScope scope)
    {
        var result = new List<SettingValue>();
        foreach (var definition in _definitions.Values)
        {
            var value = await GetOrNullAsync(definition.Name, scope);
            result.Add(new SettingValue(definition.Name, value));
        }
        return result;
    }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public async Task<string?> GetOrNullAsync(string name, SettingScope scope = SettingScope.Application)
    {
        if (!_definitions.TryGetValue(name, out var definition))
        {
            throw new XiHanException($"Setting '{name}' is not defined.");
        }

        string? value = null;
        foreach (var provider in definition.Providers)
        {
            value = await provider.GetOrNullAsync(definition);
            if (value is not null)
            {
                break;
            }
        }

        value ??= await _settingStore.GetOrNullAsync(name, definition.Name, null) ?? definition.DefaultValue;

        if (definition.IsEncrypted && !string.IsNullOrEmpty(value))
        {
            value = DecryptValue(value);
        }

        return value;
    }

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public async Task SetValueAsync(string name, string? value, SettingScope scope = SettingScope.Application)
    {
        if (!_definitions.TryGetValue(name, out var definition))
        {
            throw new XiHanException($"设置 '{name}' 没有定义");
        }

        // 数据验证
        if (definition.Validator is not null && !definition.Validator(value))
        {
            throw new XiHanException($"设置 '{name}' 的值无效");
        }

        // 敏感数据加密处理
        if (definition.IsEncrypted && !string.IsNullOrEmpty(value))
        {
            value = EncryptValue(value);
        }

        await _settingStore.SetValueAsync(name, value, definition.Name, null);

        // 触发变更事件
        OnSettingChanged?.Invoke(this, new SettingChangedEventArgs(name, scope, value));
    }

    /// <summary>
    /// 加密值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string EncryptValue(string value)
    {
        return AesHelper.Encrypt(value, "dasfdsafwerfawfgafdfd");
    }

    /// <summary>
    /// 解密值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string DecryptValue(string value)
    {
        return AesHelper.Decrypt(value, "dasfdsafwerfawfgafdfd");
    }
}
