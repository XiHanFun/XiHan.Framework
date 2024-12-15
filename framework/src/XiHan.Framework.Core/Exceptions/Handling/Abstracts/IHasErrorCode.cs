#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasErrorCode
// Guid:8fecb332-8ee4-492c-a404-3d1ead7f2bfd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 0:53:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Exceptions.Handling.Abstracts;

/// <summary>
/// 异常代码接口
/// </summary>
public interface IHasErrorCode
{
    /// <summary>
    /// 异常代码
    /// </summary>
    string? Code { get; }
}
