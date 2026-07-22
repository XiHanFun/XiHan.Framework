// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Events;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Settings;

/// <summary>
/// 设置管理器
/// </summary>
public class SettingManager : ISettingManager, IScopedDependency
{
    private const string GlobalProviderName = "G";
    private const string TenantProviderName = "T";
    private const string UserProviderName = "U";

    private readonly ILogger<SettingManager> _logger;
    private readonly ISettingStore _settingStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingDefinitionManager _definitionManager;
    private readonly XiHanAesOptions _aesOptions;

    // 运行时通过 AddDefinition 手动追加的定义（覆盖同名的 provider 定义）
    private readonly ConcurrentDictionary<string, SettingDefinition> _definitions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="settingStore"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="definitionManager">设置定义管理器（汇总所有 <see cref="ISettingDefinitionProvider"/> 的定义）</param>
    /// <param name="aesOptions">加密设置项（节名 <see cref="XiHanAesOptions.SectionName"/>）</param>
    public SettingManager(
        ILogger<SettingManager> logger,
        ISettingStore settingStore,
        IServiceProvider serviceProvider,
        ISettingDefinitionManager definitionManager,
        IOptions<XiHanAesOptions> aesOptions)
    {
        _logger = logger;
        _settingStore = settingStore;
        _serviceProvider = serviceProvider;
        _definitionManager = definitionManager;
        _aesOptions = aesOptions.Value;
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
        return AllDefinitions().Where(d => d.Group == group);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="scope"></param>
    /// <returns></returns>
    public async Task<List<SettingValue>> GetAllValuesAsync(SettingScope scope)
    {
        var result = new List<SettingValue>();
        foreach (var definition in AllDefinitions())
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
        if (!TryGetDefinition(name, out var definition))
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

        value ??= await _settingStore.GetOrNullAsync(name, GlobalProviderName, null) ?? definition.DefaultValue;

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
        if (!TryGetDefinition(name, out var definition))
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

        var (providerName, providerKey) = ResolveProvider(scope);
        if (string.IsNullOrWhiteSpace(value))
        {
            await _settingStore.DeleteAsync(name, providerName, providerKey);
        }
        else
        {
            await _settingStore.SetAsync(name, value, providerName, providerKey);
        }

        // 触发变更事件
        OnSettingChanged?.Invoke(this, new SettingChangedEventArgs(name, scope, value));
    }

    /// <summary>
    /// 解析设置定义：先查运行时手动追加的，再查定义提供者汇总表
    /// </summary>
    private bool TryGetDefinition(string name, out SettingDefinition definition)
    {
        if (_definitions.TryGetValue(name, out definition!))
        {
            return true;
        }

        var fromProviders = _definitionManager.GetOrNull(name);
        if (fromProviders is not null)
        {
            definition = fromProviders;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>
    /// 全部设置定义（提供者定义 + 运行时手动追加，同名以手动追加为准）
    /// </summary>
    private IEnumerable<SettingDefinition> AllDefinitions()
    {
        var map = new Dictionary<string, SettingDefinition>();
        foreach (var definition in _definitionManager.GetAll())
        {
            map[definition.Name] = definition;
        }
        foreach (var definition in _definitions.Values)
        {
            map[definition.Name] = definition;
        }
        return map.Values;
    }

    /// <summary>
    /// 解析设置提供者信息
    /// </summary>
    /// <param name="scope"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    private (string ProviderName, string? ProviderKey) ResolveProvider(SettingScope scope)
    {
        if (scope == SettingScope.Application)
        {
            return (GlobalProviderName, null);
        }

        var currentUser = _serviceProvider.GetService(typeof(ICurrentUser)) as ICurrentUser;
        if (scope == SettingScope.User || scope == SettingScope.Session)
        {
            if (currentUser?.UserId is null)
            {
                throw new XiHanException("当前上下文无用户信息，无法写入用户级设置");
            }

            return (UserProviderName, currentUser.UserId.Value.ToString());
        }

        if (scope == SettingScope.Tenant)
        {
            if (currentUser?.TenantId is null)
            {
                throw new XiHanException("当前上下文无租户信息，无法写入租户级设置");
            }

            return (TenantProviderName, currentUser.TenantId.Value.ToString());
        }

        return (GlobalProviderName, null);
    }

    /// <summary>
    /// 加密值（密钥来自 <see cref="XiHanAesOptions.Key"/>，AES 的 Key/IV 由该密钥经 PBKDF2 派生）
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException">未配置加密密钥时抛出，绝不退回内置占位密钥</exception>
    private string EncryptValue(string value)
    {
        return AesHelper.Encrypt(value, GetAesKey());
    }

    /// <summary>
    /// 解密值（密钥来自 <see cref="XiHanAesOptions.Key"/>）
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException">未配置加密密钥时抛出</exception>
    private string DecryptValue(string value)
    {
        return AesHelper.Decrypt(value, GetAesKey());
    }

    /// <summary>
    /// 获取加密密钥，未配置则 fail-closed 直接拒绝
    /// </summary>
    private string GetAesKey()
    {
        if (string.IsNullOrEmpty(_aesOptions.Key))
        {
            throw new XiHanException($"启用了加密设置但未配置密钥，请在配置节 '{XiHanAesOptions.SectionName}' 下设置 'Key'。");
        }

        return _aesOptions.Key;
    }
}
