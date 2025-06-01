#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentTenant
// Guid:2c06b980-4b8c-4048-995d-49ce8ccdf02c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:58:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 当前租户接口
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// 是否可用
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 当前租户ID
    /// </summary>
    long? Id { get; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// 更改当前租户信息
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    IDisposable Change(long? id, string? name = null);
}
