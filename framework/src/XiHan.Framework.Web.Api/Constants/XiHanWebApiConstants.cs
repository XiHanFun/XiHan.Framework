#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebApiConstants
// Guid:7f0f2a13-5a8f-4ca3-9f25-89d93c252fc8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
}
