#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonBase
// Guid:b8f45c21-d35f-4a29-84b7-28fd84218119
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/15 10:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Dynamic;
using System.Text.Json;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 基类，提供共同的实现和扩展方法
/// </summary>
public abstract class DynamicJsonBase : DynamicObject, IDynamicJson
{
    /// <summary>
    /// 获取原始值
    /// </summary>
    public abstract object? Value { get; }

    /// <summary>
    /// 是否为空
    /// </summary>
    public abstract bool IsEmpty { get; }

    /// <summary>
    /// 转换为 JSON 字符串
    /// </summary>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public virtual string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonHelper.Serialize(Value, options);
    }

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public virtual T ToValue<T>(T defaultValue = default!)
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
    /// 隐式转换为 dynamic
    /// </summary>
    /// <returns>动态对象</returns>
    public virtual dynamic AsDynamic()
    {
        return this;
    }

    /// <summary>
    /// 隐式转换为 dynamic
    /// </summary>
    /// <param name="obj">动态 JSON 对象</param>
    public static implicit operator DynamicObject(DynamicJsonBase obj)
    {
        return obj;
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return ToJson();
    }

    /// <summary>
    /// 提供便捷的点语法访问属性的扩展方法
    /// 使用方式：var status = obj.GetProperty("status").AsString();
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值的动态包装</returns>
    public virtual DynamicJsonValue GetProperty(string propertyName)
    {
        if (this is DynamicJsonObject obj && obj.TryGetValue(propertyName, out var value))
        {
            return new DynamicJsonValue(value);
        }
        return new DynamicJsonValue(null);
    }

    /// <summary>
    /// 设置属性值
    /// 使用方式：obj.SetProperty("status", "success");
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    public virtual void SetProperty(string propertyName, object? value)
    {
        if (this is DynamicJsonObject obj)
        {
            obj[propertyName] = value;
        }
    }

    /// <summary>
    /// 检查是否包含指定属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否包含</returns>
    public virtual bool HasProperty(string propertyName)
    {
        if (this is DynamicJsonObject obj)
        {
            return obj.ContainsKey(propertyName);
        }
        return false;
    }
} 
