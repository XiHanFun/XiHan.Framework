#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISoftDelete
// Guid:3959e48c-3240-42a8-9750-a40f1297b667
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 3:41:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 软删除接口
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// 实体是否已删除
    /// </summary>
    bool IsDeleted { get; }
}