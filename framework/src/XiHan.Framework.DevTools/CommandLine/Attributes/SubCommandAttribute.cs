// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
