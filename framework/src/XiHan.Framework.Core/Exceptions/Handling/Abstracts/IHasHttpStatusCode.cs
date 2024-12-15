﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasHttpStatusCode
// Guid:7be2629d-ca8d-4644-a037-35693ef53697
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 0:57:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Exceptions.Handling.Abstracts;

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
