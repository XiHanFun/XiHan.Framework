#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonProperty
// Guid:f91425f4-23b3-42b7-b7ac-d96eef65eb2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 15:38:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Text.Json;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 属性，类似 Newtonsoft.Json 的 JProperty
/// </summary>
[DebuggerDisplay("Name = {Name}, Value = {Value}")]
public class DynamicJsonProperty
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    public DynamicJsonProperty(string name, object? value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// 属性名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 属性值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 获取值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public T GetValue<T>(T defaultValue = default!)
    {
        try
        {
            if (Value is T directValue)
            {
                return directValue;
            }

            if (Value == null)
            {
                return defaultValue;
            }

            // 尝试类型转换
            if (typeof(T) == typeof(string))
            {
                return (T)(object)Value.ToString()!;
            }

            // 尝试 JSON 序列化/反序列化转换
            var json = JsonSerializer.Serialize(Value);
            var result = JsonSerializer.Deserialize<T>(json);
            return result != null ? result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"{Name}: {Value?.ToString() ?? "null"}";
    }

    /// <summary>
    /// 判断相等性
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>是否相等</returns>
    public override bool Equals(object? obj)
    {
        return obj is DynamicJsonProperty other &&
               Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
               Equals(Value, other.Value);
    }

    /// <summary>
    /// 获取哈希码
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Name.ToLowerInvariant(), Value);
    }
}
