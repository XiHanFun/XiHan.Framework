// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
