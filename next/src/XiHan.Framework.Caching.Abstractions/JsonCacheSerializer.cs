// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// System.Text.Json 序列化器。选项必须带 <see cref="JsonSerializerOptions.TypeInfoResolver"/>（源生成上下文或其组合），类型信息经
/// <see cref="JsonSerializerOptions.GetTypeInfo"/> 取得，AOT 下无需反射；未登记的类型抛出 <see cref="NotSupportedException"/> 而不是静默降级。
/// 只在允许反射的运行时才可用 <see cref="CreateReflectionBased"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// 用带类型解析器的选项创建；没有解析器抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.TypeInfoResolver is not null
            ? options
            : throw new ArgumentException("JsonSerializerOptions 必须设置 TypeInfoResolver（源生成上下文），否则无法在不依赖反射的情况下取得类型信息。", nameof(options));
    }

    /// <summary>
    /// 创建按反射解析类型的序列化器（Web 默认：camelCase、大小写不敏感）。不能用于 Native AOT。
    /// </summary>
    [RequiresUnreferencedCode("按反射序列化需要保留被缓存类型的成员。")]
    [RequiresDynamicCode("按反射序列化可能需要运行时生成代码。")]
    public static JsonCacheSerializer CreateReflectionBased()
        => new(new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, _options.GetTypeInfo(typeof(T)));

    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlyMemory<byte> data)
        => JsonSerializer.Deserialize(data.Span, _options.GetTypeInfo(typeof(T))) is T value
            ? value
            : throw new InvalidOperationException($"缓存数据不能还原为 {typeof(T).Name}。");
}
