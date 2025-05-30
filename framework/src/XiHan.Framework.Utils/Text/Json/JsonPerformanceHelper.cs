#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonPerformanceHelper
// Guid:b2c3d4e5-f6g7-8901-bcde-f23456789012
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2024-12-19 上午 11:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Text.Json;

/// <summary>
/// JSON 性能优化帮助类
/// </summary>
public static class JsonPerformanceHelper
{
    private static readonly JsonSerializerOptions _defaultOptions = JsonSerializerOptionsHelper.DefaultJsonSerializerOptions;

    #region 高性能序列化

    /// <summary>
    /// 使用 ArrayPool 进行高性能序列化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string SerializeWithArrayPool<T>(T obj, JsonSerializerOptions? options = null)
    {
        if (obj == null) return "null";

        options ??= _defaultOptions;
        
        var bufferWriter = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(bufferWriter);
        
        JsonSerializer.Serialize(writer, obj, options);
        
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// 使用预分配缓冲区进行序列化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="buffer">预分配的缓冲区</param>
    /// <param name="options">序列化选项</param>
    /// <returns>写入的字节数</returns>
    public static int SerializeToBuffer<T>(T obj, Span<byte> buffer, JsonSerializerOptions? options = null)
    {
        if (obj == null)
        {
            var nullBytes = "null"u8;
            nullBytes.CopyTo(buffer);
            return nullBytes.Length;
        }

        options ??= _defaultOptions;
        
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, obj, options);
        
        return (int)writer.BytesCommitted;
    }

    /// <summary>
    /// 序列化到 ReadOnlyMemory
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字节数据</returns>
    public static ReadOnlyMemory<byte> SerializeToMemory<T>(T obj, JsonSerializerOptions? options = null)
    {
        if (obj == null) return "null"u8.ToArray();

        options ??= _defaultOptions;
        return JsonSerializer.SerializeToUtf8Bytes(obj, options);
    }

    #endregion

    #region 高性能反序列化

    /// <summary>
    /// 从 ReadOnlySpan 反序列化
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="utf8Json">UTF-8 JSON 数据</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? DeserializeFromSpan<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions? options = null)
    {
        if (utf8Json.IsEmpty) return default;

        options ??= _defaultOptions;
        return JsonSerializer.Deserialize<T>(utf8Json, options);
    }

    /// <summary>
    /// 从 ReadOnlyMemory 反序列化
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="utf8Json">UTF-8 JSON 数据</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? DeserializeFromMemory<T>(ReadOnlyMemory<byte> utf8Json, JsonSerializerOptions? options = null)
    {
        if (utf8Json.IsEmpty) return default;

        options ??= _defaultOptions;
        return JsonSerializer.Deserialize<T>(utf8Json.Span, options);
    }

    /// <summary>
    /// 使用 Utf8JsonReader 进行流式反序列化
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="utf8Json">UTF-8 JSON 数据</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? DeserializeWithReader<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions? options = null)
    {
        if (utf8Json.IsEmpty) return default;

        options ??= _defaultOptions;
        var reader = new Utf8JsonReader(utf8Json);
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    #endregion

    #region 流式处理

    /// <summary>
    /// 流式写入 JSON 数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="stream">输出流</param>
    /// <param name="items">要写入的元素</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteJsonArrayAsync<T>(Stream stream, IAsyncEnumerable<T> items, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= _defaultOptions;
        
        await using var writer = new Utf8JsonWriter(stream);
        
        writer.WriteStartArray();
        
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            JsonSerializer.Serialize(writer, item, options);
            await writer.FlushAsync(cancellationToken);
        }
        
        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// 流式读取 JSON 数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="stream">输入流</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步枚举的元素</returns>
    public static async IAsyncEnumerable<T?> ReadJsonArrayAsync<T>(Stream stream, JsonSerializerOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= _defaultOptions;
        
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var json = element.GetRawText();
            yield return JsonSerializer.Deserialize<T>(json, options);
        }
    }

    /// <summary>
    /// 流式处理大型 JSON 对象
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="propertyHandler">属性处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task ProcessLargeJsonAsync(Stream stream, Func<string, JsonElement, Task> propertyHandler, CancellationToken cancellationToken = default)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await propertyHandler(property.Name, property.Value);
        }
    }

    #endregion

    #region 内存优化

    /// <summary>
    /// 使用对象池进行序列化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string SerializeWithPool<T>(T obj, int bufferSize = 4096, JsonSerializerOptions? options = null)
    {
        if (obj == null) return "null";

        options ??= _defaultOptions;
        
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var bufferWriter = new ArrayBufferWriter<byte>(buffer);
            using var writer = new Utf8JsonWriter(bufferWriter);
            
            JsonSerializer.Serialize(writer, obj, options);
            
            return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 批量序列化优化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="objects">对象集合</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串数组</returns>
    public static string[] BatchSerialize<T>(IEnumerable<T> objects, JsonSerializerOptions? options = null)
    {
        if (objects == null) return Array.Empty<string>();

        options ??= _defaultOptions;
        
        var objectList = objects.ToList();
        var results = new string[objectList.Count];
        
        // 预分配缓冲区
        const int bufferSize = 8192;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        try
        {
            for (var i = 0; i < objectList.Count; i++)
            {
                var bufferWriter = new ArrayBufferWriter<byte>(buffer);
                using var writer = new Utf8JsonWriter(bufferWriter);
                
                JsonSerializer.Serialize(writer, objectList[i], options);
                results[i] = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
                
                bufferWriter.Clear();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        
        return results;
    }

    #endregion

    #region 类型信息缓存

    private static readonly Dictionary<Type, JsonTypeInfo> _typeInfoCache = new();
    private static readonly object _cachelock = new();

    /// <summary>
    /// 获取缓存的类型信息
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="options">序列化选项</param>
    /// <returns>类型信息</returns>
    public static JsonTypeInfo<T> GetCachedTypeInfo<T>(JsonSerializerOptions? options = null)
    {
        options ??= _defaultOptions;
        var type = typeof(T);
        
        lock (_cachelock)
        {
            if (_typeInfoCache.TryGetValue(type, out var cachedInfo))
            {
                return (JsonTypeInfo<T>)cachedInfo;
            }
            
            var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<T>(options);
            _typeInfoCache[type] = typeInfo;
            return typeInfo;
        }
    }

    /// <summary>
    /// 使用缓存的类型信息进行序列化
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string SerializeWithCachedTypeInfo<T>(T obj, JsonSerializerOptions? options = null)
    {
        if (obj == null) return "null";

        var typeInfo = GetCachedTypeInfo<T>(options);
        return JsonSerializer.Serialize(obj, typeInfo);
    }

    /// <summary>
    /// 使用缓存的类型信息进行反序列化
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? DeserializeWithCachedTypeInfo<T>(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;

        var typeInfo = GetCachedTypeInfo<T>(options);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    /// <summary>
    /// 清除类型信息缓存
    /// </summary>
    public static void ClearTypeInfoCache()
    {
        lock (_cachelock)
        {
            _typeInfoCache.Clear();
        }
    }

    #endregion

    #region 性能测量

    /// <summary>
    /// 测量序列化性能
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="iterations">迭代次数</param>
    /// <param name="options">序列化选项</param>
    /// <returns>性能测量结果</returns>
    public static PerformanceResult MeasureSerializationPerformance<T>(T obj, int iterations = 1000, JsonSerializerOptions? options = null)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        options ??= _defaultOptions;
        
        // 预热
        for (var i = 0; i < 10; i++)
        {
            JsonSerializer.Serialize(obj, options);
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var startMemory = GC.GetTotalMemory(false);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (var i = 0; i < iterations; i++)
        {
            JsonSerializer.Serialize(obj, options);
        }
        
        stopwatch.Stop();
        var endMemory = GC.GetTotalMemory(false);
        
        return new PerformanceResult
        {
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Iterations = iterations,
            MemoryUsed = endMemory - startMemory,
            AverageTimePerOperation = (double)stopwatch.ElapsedMilliseconds / iterations
        };
    }

    /// <summary>
    /// 测量反序列化性能
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="iterations">迭代次数</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>性能测量结果</returns>
    public static PerformanceResult MeasureDeserializationPerformance<T>(string json, int iterations = 1000, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON 字符串不能为空", nameof(json));

        options ??= _defaultOptions;
        
        // 预热
        for (var i = 0; i < 10; i++)
        {
            JsonSerializer.Deserialize<T>(json, options);
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var startMemory = GC.GetTotalMemory(false);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (var i = 0; i < iterations; i++)
        {
            JsonSerializer.Deserialize<T>(json, options);
        }
        
        stopwatch.Stop();
        var endMemory = GC.GetTotalMemory(false);
        
        return new PerformanceResult
        {
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Iterations = iterations,
            MemoryUsed = endMemory - startMemory,
            AverageTimePerOperation = (double)stopwatch.ElapsedMilliseconds / iterations
        };
    }

    #endregion
}

/// <summary>
/// 性能测量结果
/// </summary>
public class PerformanceResult
{
    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 迭代次数
    /// </summary>
    public int Iterations { get; set; }

    /// <summary>
    /// 内存使用量（字节）
    /// </summary>
    public long MemoryUsed { get; set; }

    /// <summary>
    /// 平均每次操作耗时（毫秒）
    /// </summary>
    public double AverageTimePerOperation { get; set; }

    /// <summary>
    /// 每秒操作数
    /// </summary>
    public double OperationsPerSecond => Iterations / (ElapsedMilliseconds / 1000.0);

    /// <summary>
    /// 格式化输出结果
    /// </summary>
    /// <returns>格式化的结果字符串</returns>
    public override string ToString()
    {
        return $"总耗时: {ElapsedMilliseconds}ms, 迭代次数: {Iterations}, " +
               $"平均耗时: {AverageTimePerOperation:F3}ms, " +
               $"每秒操作数: {OperationsPerSecond:F0}, " +
               $"内存使用: {MemoryUsed / 1024.0:F2}KB";
    }
} 
