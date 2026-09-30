// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Authorization.SqlSugar.Mapping;

/// <summary>
/// JSON 列的序列化辅助
/// </summary>
internal static class JsonColumn
{
    /// <summary>
    /// 序列化为 JSON 文本，值为空时返回空
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="value">要序列化的值</param>
    /// <returns>JSON 文本</returns>
    public static string? SerializeOrNull<T>(T? value) where T : class
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 从 JSON 文本反序列化，文本为空时返回空
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="json">JSON 文本</param>
    /// <returns>反序列化后的值</returns>
    public static T? DeserializeOrNull<T>(string? json) where T : class
    {
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json);
    }
}
