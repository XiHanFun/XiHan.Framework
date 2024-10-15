#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumExtensions
// Guid:23f4fdd1-650e-49f7-bdc6-7ba00110a2ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-09 上午 12:55:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Reflection;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 枚举拓展类
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 根据名称匹配枚举
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static TEnum ToEnum<TEnum>(this string name) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(name, out var result) ? result : throw new ArgumentException("无效的枚举值！", nameof(name));
    }

    /// <summary>
    /// 根据键获取单个枚举的值
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static int GetValue(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field == null ? throw new ArgumentException("无效的枚举！", nameof(keyEnum)) : (int)field.GetRawConstantValue()!;
    }

    /// <summary>
    /// 根据键获取单个枚举的描述信息
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static string GetDescription(this Enum keyEnum)
    {
        var field = keyEnum.GetType().GetField(keyEnum.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? throw new ArgumentException("枚举无描述信息！", nameof(keyEnum));
    }

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    public static IEnumerable<EnumDescInfo> GetEnumInfos(this Type enumType)
    {
        var result = new List<EnumDescInfo>();
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (!field.FieldType.IsEnum)
                continue;

            var desc = string.Empty;
            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute description)
                desc = description.Description;

            result.Add(new EnumDescInfo
            {
                Key = field.Name,
                Value = (int)field.GetRawConstantValue()!,
                Label = desc
            });
        }
        return result;
    }

    /// <summary>
    /// 枚举的值与描述转为字典类型
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    public static IDictionary<int, string> GetEnumInfoDictionary(this Type enumType)
    {
        var result = new Dictionary<int, string>();
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (!field.FieldType.IsEnum)
                continue;

            var desc = string.Empty;
            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute description)
                desc = description.Description;

            result.Add((int)field.GetRawConstantValue()!, desc);
        }
        return result;
    }
}

/// <summary>
/// EnumDescInfo
/// </summary>
public record EnumDescInfo
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Label { get; init; } = string.Empty;
}