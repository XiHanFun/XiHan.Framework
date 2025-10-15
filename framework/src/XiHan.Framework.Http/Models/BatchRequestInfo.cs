#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BatchRequestInfo
// Guid:f2a32abc-617f-49aa-8abf-c39b1d22f0ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/5 1:21:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Models;

/// <summary>
/// 批量请求信息
/// </summary>
public class BatchRequestInfo
{
    /// <summary>
    /// 请求方法
    /// </summary>
    public HttpMethod Method { get; set; } = HttpMethod.Get;

    /// <summary>
    /// 请求Url
    /// </summary>
    public string Url { get; set; } = null!;

    /// <summary>
    /// 请求内容
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    /// 请求选项
    /// </summary>
    public XiHanHttpRequestOptions? Options { get; set; }

    /// <summary>
    /// 请求标识
    /// </summary>
    public string? Id { get; set; }
}
