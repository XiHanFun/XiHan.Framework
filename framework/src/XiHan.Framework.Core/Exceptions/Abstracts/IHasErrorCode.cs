// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Exceptions.Abstracts;

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
