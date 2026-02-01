#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CreationDtoBase
// Guid:f5919add-1e73-4551-9af9-ce67cb2ac2d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/11/16 02:32:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 创建 DTO 基类
/// </summary>
public abstract class CreationDtoBase
{
}

/// <summary>
/// 泛型主键创建 DTO 基类
/// </summary>
public abstract class CreationDtoBase<TKey> : CreationDtoBase
    where TKey : IEquatable<TKey>
{
}
