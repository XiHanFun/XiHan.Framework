#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SerializeHelper
// Guid:1345864e-97d1-4fbf-8f3e-5f9d5d51176e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/3/26 5:26:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Utils.Extensions.System.Text;

namespace XiHan.Framework.Utils.Text.Json.Serialization;

/// <summary>
/// 序列化帮助类
/// </summary>
public static class SerializeHelper
{
    /// <summary>
    /// 序列化为Json
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public static string SerializeTo(object item)
    {
        return JsonSerializer.Serialize(item, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
    }

    /// <summary>
    /// 反序列化为对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="jsonString"></param>
    /// <returns></returns>
    public static T? DeserializeTo<T>(string jsonString)
    {
        return JsonSerializer.Deserialize<T>(jsonString, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
    }

    /// <summary>
    /// 反序列化为对象
    /// </summary>
    /// <param name="jsonString"></param>
    /// <returns></returns>
    public static object? DeserializeTo(string jsonString)
    {
        return JsonSerializer.Deserialize(jsonString.ToStream(),
            JsonTypeInfo.CreateJsonTypeInfo(typeof(object), JsonSerializerOptionsHelper.DefaultJsonSerializerOptions));
    }
}
