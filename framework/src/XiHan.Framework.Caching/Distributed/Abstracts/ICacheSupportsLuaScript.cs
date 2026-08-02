// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 缓存 Lua 脚本支持接口（基于规范化键）
/// </summary>
/// <remarks>
/// 能力接口：只有具备脚本执行能力的缓存实现才实现它，调用方经类型判断按需使用。
/// 签名中不出现任何具体缓存客户端的类型，参数与返回值都以中立形式表达。
/// </remarks>
public interface ICacheSupportsLuaScript
{
    /// <summary>
    /// 执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数值集合</param>
    /// <returns>脚本执行结果</returns>
    CacheScriptResult ScriptEvaluate(string script, string[]? keys = null, object?[]? values = null);

    /// <summary>
    /// 异步执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数值集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>脚本执行结果</returns>
    Task<CacheScriptResult> ScriptEvaluateAsync(string script, string[]? keys = null, object?[]? values = null, CancellationToken token = default);
}
