#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpdateDtoBase
// Guid:fa736f54-8e0a-4034-84e8-29955843fb0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/11/16 02:34:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Application.Contracts.Dtos;

/// <summary>
/// 更新 DTO 基类
/// </summary>
public abstract class UpdateDtoBase
{
}

/// <summary>
/// 泛型主键更新 DTO 基类
/// </summary>
public abstract class UpdateDtoBase<TKey> : UpdateDtoBase
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; set; } = default!;
}
