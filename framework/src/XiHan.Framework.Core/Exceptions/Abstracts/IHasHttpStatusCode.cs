// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Exceptions.Abstracts;

/// <summary>
/// 网络请求状态码接口
/// </summary>
public interface IHasHttpStatusCode
{
    /// <summary>
    /// 网络请求状态码
    /// </summary>
    int HttpStatusCode { get; }
}
