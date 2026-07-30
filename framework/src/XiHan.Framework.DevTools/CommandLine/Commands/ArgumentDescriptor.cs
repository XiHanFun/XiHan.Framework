// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.DevTools.CommandLine.Attributes;

namespace XiHan.Framework.DevTools.CommandLine.Commands;

/// <summary>
/// 参数描述符
/// </summary>
public class ArgumentDescriptor
{
    /// <summary>
    /// 创建参数描述符
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="argumentAttr">参数属性</param>
    public ArgumentDescriptor(MemberInfo member, CommandArgumentAttribute argumentAttr)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));

        Position = argumentAttr.Position;
        Name = argumentAttr.Name;
        Description = argumentAttr.Description;
        Required = argumentAttr.Required;
        DefaultValue = argumentAttr.DefaultValue;
        AllowMultiple = argumentAttr.AllowMultiple;

        MemberType = member switch
        {
            PropertyInfo prop => prop.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new ArgumentException("成员必须是属性或字段")
        };
    }

    /// <summary>
    /// 参数位置
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; }

    /// <summary>
    /// 成员信息
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// 成员类型
    /// </summary>
    public Type MemberType { get; }
}
