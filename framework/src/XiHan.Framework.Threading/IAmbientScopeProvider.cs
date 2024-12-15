#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAmbientScopeProvider
// Guid:dd4544fe-7812-412f-94ca-b0bc5c01ab4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:02:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
