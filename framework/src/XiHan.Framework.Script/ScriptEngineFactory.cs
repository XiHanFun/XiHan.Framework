#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptEngineFactory
// Guid:f6g7h8i9-j0k1-2345-ghij-567890123456
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/18 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script;

/// <summary>
/// 脚本引擎工厂
/// 提供脚本引擎的创建和管理功能
/// </summary>
public static class ScriptEngineFactory
{
    private static readonly Dictionary<string, IScriptEngine> _engines = [];
    private static readonly Lock _lockObject = new();

    /// <summary>
    /// 创建默认脚本引擎
    /// </summary>
    /// <returns>脚本引擎实例</returns>
    public static IScriptEngine CreateDefault()
    {
        return new ScriptEngine();
    }

    /// <summary>
    /// 创建预配置的脚本引擎
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>脚本引擎实例</returns>
    public static IScriptEngine Create(Action<ScriptOptions>? configure = null)
    {
        var engine = new ScriptEngine();

        if (configure != null)
        {
            // 这里可以扩展为支持引擎级别的配置
            // 目前暂时保留接口，为将来扩展做准备
        }

        return engine;
    }

    /// <summary>
    /// 获取或创建命名的脚本引擎
    /// </summary>
    /// <param name="name">引擎名称</param>
    /// <param name="configure">配置委托</param>
    /// <returns>脚本引擎实例</returns>
    public static IScriptEngine GetOrCreate(string name, Action<ScriptOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("引擎名称不能为空", nameof(name));
        }

        lock (_lockObject)
        {
            if (_engines.TryGetValue(name, out var existingEngine))
            {
                return existingEngine;
            }

            var newEngine = Create(configure);
            _engines[name] = newEngine;
            return newEngine;
        }
    }

    /// <summary>
    /// 释放指定名称的脚本引擎
    /// </summary>
    /// <param name="name">引擎名称</param>
    /// <returns>是否成功释放</returns>
    public static bool Release(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_lockObject)
        {
            if (_engines.TryGetValue(name, out var engine))
            {
                if (engine is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                return _engines.Remove(name);
            }
        }

        return false;
    }

    /// <summary>
    /// 释放所有脚本引擎
    /// </summary>
    public static void ReleaseAll()
    {
        lock (_lockObject)
        {
            foreach (var engine in _engines.Values)
            {
                if (engine is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _engines.Clear();
        }
    }

    /// <summary>
    /// 获取所有注册的引擎名称
    /// </summary>
    /// <returns>引擎名称集合</returns>
    public static IEnumerable<string> GetEngineNames()
    {
        lock (_lockObject)
        {
            return [.. _engines.Keys];
        }
    }

    /// <summary>
    /// 获取指定名称的引擎统计信息
    /// </summary>
    /// <param name="name">引擎名称</param>
    /// <returns>统计信息</returns>
    public static EngineStatistics? GetStatistics(string name)
    {
        lock (_lockObject)
        {
            return _engines.TryGetValue(name, out var engine) ? engine.GetStatistics() : null;
        }
    }

    /// <summary>
    /// 获取所有引擎的统计信息
    /// </summary>
    /// <returns>引擎统计信息字典</returns>
    public static Dictionary<string, EngineStatistics> GetAllStatistics()
    {
        lock (_lockObject)
        {
            return _engines.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetStatistics());
        }
    }

    /// <summary>
    /// 清除所有引擎的缓存
    /// </summary>
    public static void ClearAllCaches()
    {
        lock (_lockObject)
        {
            foreach (var engine in _engines.Values)
            {
                engine.ClearCache();
            }
        }
    }

    /// <summary>
    /// 清除指定引擎的缓存
    /// </summary>
    /// <param name="name">引擎名称</param>
    /// <returns>是否成功清除</returns>
    public static bool ClearCache(string name)
    {
        lock (_lockObject)
        {
            if (_engines.TryGetValue(name, out var engine))
            {
                engine.ClearCache();
                return true;
            }
        }
        return false;
    }
}
