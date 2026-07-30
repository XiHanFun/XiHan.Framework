// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
