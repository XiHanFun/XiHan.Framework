#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateEngineRegistry
// Guid:89a9fa9f-fe07-4716-954d-f88d09727649
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 模板引擎注册表实现
/// </summary>
public class TemplateEngineRegistry : ITemplateEngineRegistry
{
    private readonly ConcurrentDictionary<string, object> _engines = new();
    private readonly ConcurrentDictionary<Type, string> _defaultEngines = new();

    /// <summary>
    /// 获取引擎数量
    /// </summary>
    /// <returns>引擎数量</returns>
    public int Count => _engines.Count;

    /// <summary>
    /// 注册模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <param name="engine">模板引擎实例</param>
    public void RegisterEngine<T>(string engineName, ITemplateEngine<T> engine)
    {
        var key = GenerateKey<T>(engineName);
        _engines[key] = engine;
    }

    /// <summary>
    /// 获取模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>模板引擎实例</returns>
    public ITemplateEngine<T>? GetEngine<T>(string engineName)
    {
        var key = GenerateKey<T>(engineName);
        return _engines.TryGetValue(key, out var engine) ? (ITemplateEngine<T>)engine : null;
    }

    /// <summary>
    /// 获取默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <returns>默认模板引擎实例</returns>
    public ITemplateEngine<T>? GetDefaultEngine<T>()
    {
        if (_defaultEngines.TryGetValue(typeof(T), out var defaultEngineName))
        {
            return GetEngine<T>(defaultEngineName);
        }

        // 如果没有设置默认引擎，尝试查找第一个可用的引擎
        var prefix = $"{typeof(T).FullName}:";
        var firstEngine = _engines.FirstOrDefault(kvp => kvp.Key.StartsWith(prefix));
        return firstEngine.Value as ITemplateEngine<T>;
    }

    /// <summary>
    /// 设置默认模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    public void SetDefaultEngine<T>(string engineName)
    {
        _defaultEngines[typeof(T)] = engineName;
    }

    /// <summary>
    /// 获取所有已注册的引擎名称
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <returns>引擎名称集合</returns>
    public IEnumerable<string> GetEngineNames<T>()
    {
        var prefix = $"{typeof(T).FullName}:";
        return _engines.Keys
            .Where(key => key.StartsWith(prefix))
            .Select(key => key[prefix.Length..]);
    }

    /// <summary>
    /// 移除模板引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveEngine<T>(string engineName)
    {
        var key = GenerateKey<T>(engineName);
        var removed = _engines.TryRemove(key, out _);

        // 如果移除的是默认引擎，清除默认设置
        if (removed && _defaultEngines.TryGetValue(typeof(T), out var defaultEngine) && defaultEngine == engineName)
        {
            _defaultEngines.TryRemove(typeof(T), out _);
        }

        return removed;
    }

    /// <summary>
    /// 清空所有引擎
    /// </summary>
    public void Clear()
    {
        _engines.Clear();
        _defaultEngines.Clear();
    }

    /// <summary>
    /// 是否包含指定引擎
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>是否包含</returns>
    public bool ContainsEngine<T>(string engineName)
    {
        var key = GenerateKey<T>(engineName);
        return _engines.ContainsKey(key);
    }

    /// <summary>
    /// 生成引擎键
    /// </summary>
    /// <typeparam name="T">模板类型</typeparam>
    /// <param name="engineName">引擎名称</param>
    /// <returns>引擎键</returns>
    private static string GenerateKey<T>(string engineName)
    {
        return $"{typeof(T).FullName}:{engineName}";
    }
}
