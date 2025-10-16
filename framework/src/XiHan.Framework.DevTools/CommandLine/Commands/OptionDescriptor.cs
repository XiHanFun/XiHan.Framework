#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OptionDescriptor
// Guid:c4afdce4-fcdf-43fb-8aa3-5f222c2703b7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:00:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.DevTools.CommandLine.Attributes;

namespace XiHan.Framework.DevTools.CommandLine.Commands;

/// <summary>
/// 选项描述符
/// </summary>
public class OptionDescriptor
{
    /// <summary>
    /// 创建选项描述符
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="optionAttr">选项属性</param>
    public OptionDescriptor(MemberInfo member, CommandOptionAttribute optionAttr)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));

        LongName = optionAttr.LongName;
        ShortName = optionAttr.ShortName;
        Description = optionAttr.Description;
        Required = optionAttr.Required;
        DefaultValue = optionAttr.DefaultValue;
        IsSwitch = optionAttr.IsSwitch;
        AllowMultiple = optionAttr.AllowMultiple;
        MetaName = optionAttr.MetaName ?? GetDefaultMetaName();

        MemberType = member switch
        {
            PropertyInfo prop => prop.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new ArgumentException("成员必须是属性或字段")
        };

        // 自动检测布尔类型
        if (MemberType == typeof(bool) || MemberType == typeof(bool?))
        {
            IsSwitch = true;
        }
    }

    /// <summary>
    /// 长选项名称
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// 短选项名称
    /// </summary>
    public string? ShortName { get; }

    /// <summary>
    /// 选项描述
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
    /// 是否为布尔开关
    /// </summary>
    public bool IsSwitch { get; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; }

    /// <summary>
    /// 参数名称（用于帮助显示）
    /// </summary>
    public string? MetaName { get; }

    /// <summary>
    /// 成员信息
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// 成员类型
    /// </summary>
    public Type MemberType { get; }

    /// <summary>
    /// 获取选项名称列表（包含长名称和短名称）
    /// </summary>
    /// <returns>选项名称列表</returns>
    public List<string> GetNames()
    {
        var names = new List<string> { LongName };
        if (!string.IsNullOrEmpty(ShortName))
        {
            names.Add(ShortName);
        }
        return names;
    }

    /// <summary>
    /// 获取默认的参数名称
    /// </summary>
    /// <returns>参数名称</returns>
    private string GetDefaultMetaName()
    {
        if (IsSwitch)
        {
            return "";
        }

        var typeName = MemberType.Name.ToLowerInvariant();
        return typeName switch
        {
            "string" => "VALUE",
            "int32" or "int64" or "int16" => "NUMBER",
            "double" or "single" or "decimal" => "NUMBER",
            "datetime" => "DATE",
            "boolean" => "",
            _ => "VALUE"
        };
    }
}
