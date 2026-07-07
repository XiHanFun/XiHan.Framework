#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobSerializer
// Guid:94043980-c581-4455-85b8-5289af892ef8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 基于 System.Text.Json 的后台作业参数序列化器
/// </summary>
public class BackgroundJobSerializer : IBackgroundJobSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>序列化字符串</returns>
    public string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, SerializerOptions);
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="value">序列化字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public object Deserialize(string value, Type type)
    {
        return JsonSerializer.Deserialize(value, type, SerializerOptions)
            ?? throw new InvalidOperationException($"反序列化后台作业参数失败，目标类型：{type.FullName}");
    }
}
