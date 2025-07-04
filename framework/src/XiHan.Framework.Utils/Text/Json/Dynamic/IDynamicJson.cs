#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDynamicJson
// Guid:a8f45c21-d35f-4a29-84b7-28fd84218118
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/15 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 接口，定义所有动态 JSON 类的通用行为
/// </summary>
public interface IDynamicJson
{
    /// <summary>
    /// 获取原始值
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// 是否为空
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// 转换为 JSON 字符串
    /// </summary>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    string ToJson(JsonSerializerOptions? options = null);

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    T ToValue<T>(T defaultValue = default!);

    /// <summary>
    /// 隐式转换为 dynamic
    /// </summary>
    /// <returns>动态对象</returns>
    dynamic AsDynamic();
} 
