#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHybridCacheJsonSerializerFactory
// Guid:126f0e98-f081-40f5-ac65-32bf7e4e2958
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/3/12 20:51:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Serialization.Options;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存Json序列化器工厂
/// </summary>
public class XiHanHybridCacheJsonSerializerFactory : IHybridCacheSerializerFactory
{
    /// <summary>
    /// System.Text.Json序列化器选项
    /// </summary>
    protected IOptions<XiHanSystemTextJsonSerializerOptions> Options { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    public XiHanHybridCacheJsonSerializerFactory(IOptions<XiHanSystemTextJsonSerializerOptions> options)
    {
        Options = options;
    }

    /// <summary>
    /// 尝试创建序列化器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="serializer"></param>
    /// <returns></returns>
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        if (typeof(T) == typeof(string) || typeof(T) == typeof(byte[]))
        {
            serializer = null;
            return false;
        }

        serializer = new XiHanHybridCacheJsonSerializer<T>(Options.Value.JsonSerializerOptions);
        return true;
    }
}
