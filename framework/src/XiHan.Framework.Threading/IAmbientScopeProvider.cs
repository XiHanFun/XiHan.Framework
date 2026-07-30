// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Threading;

/// <summary>
/// 环境作用域提供者
/// </summary>
public interface IAmbientScopeProvider<T>
{
    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="contextKey"></param>
    /// <returns></returns>
    T? GetValue(string contextKey);

    /// <summary>
    /// 开始作用域
    /// </summary>
    /// <param name="contextKey"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    IDisposable BeginScope(string contextKey, T value);
}
