#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentTenantAccessor
// Guid:6df1fb96-f883-4025-8c5d-49cfd6dc782c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:28:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 当前租户访问器接口
/// </summary>
public interface ICurrentTenantAccessor
{
    /// <summary>
    /// 当前租户
    /// </summary>
    BasicTenantInfo? Current { get; set; }
}
