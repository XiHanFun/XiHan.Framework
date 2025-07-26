#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkAuditAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Enums;

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架审计特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrameworkAuditAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="operationType">操作类型</param>
    /// <param name="operationDescription">操作描述</param>
    /// <param name="logDetails">是否记录详细信息</param>
    public FrameworkAuditAttribute(OperationType operationType, string operationDescription = "", bool logDetails = true)
    {
        OperationType = operationType;
        OperationDescription = operationDescription;
        LogDetails = logDetails;
    }

    /// <summary>
    /// 操作类型
    /// </summary>
    public OperationType OperationType { get; }

    /// <summary>
    /// 操作描述
    /// </summary>
    public string OperationDescription { get; }

    /// <summary>
    /// 是否记录详细信息
    /// </summary>
    public bool LogDetails { get; }
}
