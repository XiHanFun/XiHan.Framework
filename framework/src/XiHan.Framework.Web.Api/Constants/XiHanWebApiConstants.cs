// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Constants;

/// <summary>
/// WebApi 常量
/// </summary>
public static class XiHanWebApiConstants
{
    /// <summary>
    /// TraceId 请求头名称
    /// </summary>
    public const string TraceIdHeaderName = "X-Trace-Id";

    /// <summary>
    /// HttpContext Items 中的 TraceId 键
    /// </summary>
    public const string TraceIdItemKey = "__XiHanTraceId";

    /// <summary>
    /// HttpContext Items 中的 RequestBody 键
    /// </summary>
    public const string RequestBodyItemKey = "__XiHanRequestBody";

    /// <summary>
    /// HttpContext Items 中的 QueryString 键
    /// </summary>
    public const string RequestQueryItemKey = "__XiHanRequestQuery";

    /// <summary>
    /// HttpContext Items 中的 RequestContext 键
    /// </summary>
    public const string RequestContextItemKey = "__XiHanRequestContext";
}
