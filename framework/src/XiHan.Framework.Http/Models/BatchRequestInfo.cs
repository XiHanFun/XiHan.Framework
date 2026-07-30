// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
