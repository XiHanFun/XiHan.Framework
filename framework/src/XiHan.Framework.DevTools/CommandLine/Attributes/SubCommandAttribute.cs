#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SubCommandAttribute
// Guid:b5b7488a-b3c5-4d8e-8049-a00d8084a600
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:02:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 子命令属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SubCommandAttribute : Attribute
{
    /// <summary>
    /// 创建子命令属性
    /// </summary>
    /// <param name="commandType">子命令类型</param>
    public SubCommandAttribute(Type commandType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
    }

    /// <summary>
    /// 子命令类型
    /// </summary>
    public Type CommandType { get; }
}
