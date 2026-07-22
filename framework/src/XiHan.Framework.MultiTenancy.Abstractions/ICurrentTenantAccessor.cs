// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
