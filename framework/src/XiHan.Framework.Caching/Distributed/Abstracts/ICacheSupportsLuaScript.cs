// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 缓存 Lua 脚本支持接口（基于规范化键）
/// </summary>
public interface ICacheSupportsLuaScript
{
    /// <summary>
    /// 执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数值集合</param>
    /// <returns>脚本执行结果</returns>
    RedisResult ScriptEvaluate(string script, string[]? keys = null, RedisValue[]? values = null);

    /// <summary>
    /// 异步执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数值集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>脚本执行结果</returns>
    Task<RedisResult> ScriptEvaluateAsync(string script, string[]? keys = null, RedisValue[]? values = null, CancellationToken token = default);
}
