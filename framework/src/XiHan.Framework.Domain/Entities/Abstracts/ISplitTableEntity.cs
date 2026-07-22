// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Entities.Abstracts;

/// <summary>
/// 分表实体标记接口
/// 分表必须实现此接口，框架会根据实体类型自动进行分表处理
/// 分表必须有 CreatedTime 属性，用于分表的时间范围划分
/// </summary>
public interface ISplitTableEntity : ICreationEntity
{
}
