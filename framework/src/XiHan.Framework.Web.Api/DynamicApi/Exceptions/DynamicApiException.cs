#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiException
// Guid:param-rule-validator-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Exceptions;

/// <summary>
/// 动态 API 异常
/// </summary>
public class DynamicApiException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiException(string message) : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
