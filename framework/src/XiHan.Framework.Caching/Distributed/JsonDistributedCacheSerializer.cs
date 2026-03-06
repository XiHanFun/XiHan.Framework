#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonDistributedCacheSerializer
// Guid:5b3f470f-dad6-4ca8-93af-9f2aa5f215ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/03 18:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 基于 System.Text.Json 的分布式缓存序列化器
/// </summary>
public class JsonDistributedCacheSerializer : IDistributedCacheSerializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    public JsonDistributedCacheSerializer(IOptions<JsonSerializerOptions> options)
    {
        _jsonSerializerOptions = options.Value;
    }

    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public byte[] Serialize<T>(T obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, _jsonSerializerOptions);
    }

    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public T Deserialize<T>(byte[] bytes)
    {
        var value = JsonSerializer.Deserialize<T>(bytes, _jsonSerializerOptions);
        return value is null
            ? throw new InvalidOperationException($"无法将缓存内容反序列化为类型 {typeof(T).FullName}")
            : value;
    }
}
