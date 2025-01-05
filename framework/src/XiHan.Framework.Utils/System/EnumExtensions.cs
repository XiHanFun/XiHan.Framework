#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumExtensions
// Guid:9ce569e4-6869-4251-8dc5-fad69e9d56e6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:56:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Reflection;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 根据名称匹配枚举
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static TEnum GetEnumByName<TEnum>(this string name)
        where TEnum : struct
    {
        var tEnum = Enum.Parse<TEnum>(name, true);
        return tEnum;
    }

    /// <summary>
    /// 根据键获取单个枚举的值
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static int GetEnumValueByKey(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field == null ? throw new ArgumentException(null, nameof(keyEnum)) : (int)field.GetRawConstantValue()!;
    }

    /// <summary>
    /// 根据键获取单个枚举的描述信息
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static string GetEnumDescriptionByKey(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field == null
            ? string.Empty
            : field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute description
                ? description.Description
                : string.Empty;
    }

    /// <summary>
    /// 根据值获取单个枚举的描述信息
    /// </summary>
    /// <param name="enumValue"></param>
    /// <returns></returns>
    public static string GetEnumDescriptionByValue<TEnum>(this object enumValue)
    {
        string? description;
        try
        {
            var tEnum = Enum.Parse(typeof(TEnum), enumValue.ParseToString()) as Enum;
            description = tEnum!.GetEnumDescriptionByKey();
        }
        catch (Exception ex)
        {
            throw new Exception("获取单个枚举的描述信息出错，" + ex.Message);
        }

        return description;
    }

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    public static IEnumerable<EnumInfo> GetEnumInfos(this Type enumType)
    {
        List<EnumInfo> result = [];
        var fields = enumType.GetFields().Skip(1).ToList();
        fields.ForEach(field =>
        {
            // 不是枚举字段不处理
            if (!field.FieldType.IsEnum)
            {
                return;
            }

            var desc = string.Empty;
            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute description)
            {
                desc = description.Description;
            }

            result.Add(new EnumInfo
            {
                Key = field.Name.ToString(),
                Value = (int)field.GetRawConstantValue()!,
                Label = desc
            });
        });
        return result;
    }

    /// <summary>
    /// 枚举的值与描述转为字典类型
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    public static Dictionary<int, string> GetEnumValueDescriptionToDictionary(this Type enumType)
    {
        Dictionary<int, string> result = [];
        var fields = enumType.GetFields().ToList();
        if (fields.Count != 0)
        {
            return result;
        }

        fields.ForEach(field =>
        {
            // 不是枚举字段不处理
            if (!field.FieldType.IsEnum)
            {
                return;
            }

            var desc = string.Empty;
            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute description)
            {
                desc = description.Description;
            }

            result.Add((int)field.GetRawConstantValue()!, desc);
        });
        return result;
    }
}

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Label { get; init; } = string.Empty;
}
