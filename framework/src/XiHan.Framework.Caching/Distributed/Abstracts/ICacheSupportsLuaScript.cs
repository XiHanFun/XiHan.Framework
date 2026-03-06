#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICacheSupportsLuaScript
// Guid:69d39b53-7e4f-4d32-b5e9-23309469af6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 17:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
