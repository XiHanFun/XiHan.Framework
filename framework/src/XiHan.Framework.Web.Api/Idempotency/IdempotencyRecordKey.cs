// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等记录键
/// </summary>
/// <param name="TenantId">租户标识，宿主为空字符串</param>
/// <param name="SubjectId">调用主体标识</param>
/// <param name="Method">HTTP 方法</param>
/// <param name="Endpoint">请求路径</param>
/// <param name="Key">调用方提供的幂等键</param>
public sealed record IdempotencyRecordKey(string TenantId, string SubjectId, string Method, string Endpoint, string Key)
{
    /// <summary>
    /// 计算记录键的 SHA-256 十六进制摘要
    /// </summary>
    /// <returns>64 位大写十六进制字符串</returns>
    public string ComputeHash()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, TenantId);
        AppendField(hash, SubjectId);
        AppendField(hash, Method);
        AppendField(hash, Endpoint);
        AppendField(hash, Key);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// 以「长度:值」追加字段
    /// </summary>
    /// <param name="hash">增量哈希</param>
    /// <param name="value">字段值</param>
    internal static void AppendField(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(Encoding.ASCII.GetBytes($"{bytes.Length}:"));
        hash.AppendData(bytes);
    }
}
