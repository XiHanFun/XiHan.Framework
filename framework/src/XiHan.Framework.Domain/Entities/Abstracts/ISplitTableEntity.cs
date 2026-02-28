#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISplitTableEntity
// Guid:8394c15b-0c6c-4adb-b245-b8c690bbb2a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 01:05:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 分表实体标记接口
/// 分表必须实现此接口，框架会根据实体类型自动进行分表处理
/// 分表必须有 CreatedTime 属性，用于分表的时间范围划分
/// </summary>
public interface ISplitTableEntity : ICreationEntity
{
}
