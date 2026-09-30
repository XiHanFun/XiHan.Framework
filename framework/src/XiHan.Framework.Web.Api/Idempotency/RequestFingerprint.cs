// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 请求摘要：HTTP 方法、路径、查询字符串与模型绑定后的动作参数
/// </summary>
public static class RequestFingerprint
{
    /// <summary>
    /// 参数中是否含幂等保护不支持的文件或流
    /// </summary>
    /// <param name="arguments">动作参数</param>
    /// <returns>含不支持的参数时为 true</returns>
    public static bool ContainsUnsupportedArgument(IDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Values.Any(value => value is Stream or IFormFile or IFormFileCollection);
    }

    /// <summary>
    /// 参数类型是否为幂等保护不支持的文件或流，或含此类公开可读属性的对象（检查一层）
    /// </summary>
    /// <param name="type">参数类型</param>
    /// <returns>不支持时为 true</returns>
    public static bool IsUnsupportedParameterType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (IsFileOrStreamType(type))
        {
            return true;
        }

        if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.CanRead && property.GetIndexParameters().Length == 0 && IsFileOrStreamType(property.PropertyType));
    }

    /// <summary>
    /// 计算请求摘要；参数序列化后超过上限时返回 false
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    /// <param name="path">请求路径</param>
    /// <param name="queryString">查询字符串</param>
    /// <param name="arguments">动作参数</param>
    /// <param name="serializerOptions">序列化配置</param>
    /// <param name="maxBytes">参数序列化后的最大字节数</param>
    /// <param name="fingerprint">摘要</param>
    /// <returns>是否计算成功</returns>
    public static bool TryCompute(
        string method,
        string path,
        string? queryString,
        IDictionary<string, object?> arguments,
        JsonSerializerOptions serializerOptions,
        int maxBytes,
        out string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var payload = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            if (value is CancellationToken)
            {
                continue;
            }

            payload[name] = value;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, serializerOptions);
        if (body.Length > maxBytes)
        {
            fingerprint = string.Empty;
            return false;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IdempotencyRecordKey.AppendField(hash, method);
        IdempotencyRecordKey.AppendField(hash, path);
        IdempotencyRecordKey.AppendField(hash, queryString ?? string.Empty);
        hash.AppendData(body);
        fingerprint = Convert.ToHexString(hash.GetHashAndReset());
        return true;
    }

    private static bool IsFileOrStreamType(Type type)
    {
        return typeof(Stream).IsAssignableFrom(type) ||
               typeof(IFormFile).IsAssignableFrom(type) ||
               typeof(IFormFileCollection).IsAssignableFrom(type) ||
               typeof(IEnumerable<IFormFile>).IsAssignableFrom(type);
    }
}
