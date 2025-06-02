#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasErrorDetails
// Guid:f7be1666-7a6f-4182-be4e-59cfd00f3dc6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 0:53:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Exceptions.Abstracts;

/// <summary>
/// 异常详情接口
/// </summary>
public interface IHasErrorDetails
{
    /// <summary>
    /// 异常详情
    /// </summary>
    string? Details { get; }
}
