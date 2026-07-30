// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Exceptions;

/// <summary>
/// 领域异常基类
/// 用于表示业务规则违反或领域逻辑错误
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DomainException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public DomainException(string message) : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="details">异常详细信息</param>
    public DomainException(string message, string? details) : base(message)
    {
        Details = details;
    }

    /// <summary>
    /// 异常详细信息
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 错误代码（可选）
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 创建领域异常
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="code">错误代码</param>
    /// <param name="details">异常详细信息</param>
    /// <returns>领域异常实例</returns>
    public static DomainException Create(string message, string? code = null, string? details = null)
    {
        return new DomainException(message, details)
        {
            Code = code
        };
    }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>异常的字符串表示</returns>
    public override string ToString()
    {
        var result = base.ToString();

        if (!string.IsNullOrEmpty(Code))
        {
            result = $"Code: {Code}\n{result}";
        }

        if (!string.IsNullOrEmpty(Details))
        {
            result = $"{result}\nDetails: {Details}";
        }

        return result;
    }
}
