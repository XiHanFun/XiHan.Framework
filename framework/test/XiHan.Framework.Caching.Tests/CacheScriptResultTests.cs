// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;
using System.Reflection;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 缓存脚本结果的测试
/// </summary>
/// <remarks>
/// 覆盖「抽象层不出现具体缓存客户端类型」这一契约，以及 Redis 返回值到中立结果的转换。
/// </remarks>
public class CacheScriptResultTests
{
    /// <summary>
    /// 分布式缓存抽象的签名中不出现任何 Redis 类型
    /// </summary>
    /// <remarks>
    /// 这是本次改动要守住的东西：核心缓存抽象一旦焊上某个客户端的类型，
    /// 换实现就要改所有调用方。用反射断言可保证以后不会被无意加回去。
    /// </remarks>
    [Fact]
    public void DistributedCacheAbstraction_ExposesNoRedisTypes()
    {
        var offenders = typeof(IDistributedCache<>).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.IsPublic)
            .Where(type => type.Namespace?.StartsWith("XiHan.Framework.Caching.Distributed.Abstracts", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods())
            .Where(method => ReferencesRedisType(method))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Distinct()
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// 空返回值转换为空结果
    /// </summary>
    [Fact]
    public void ToScriptResult_ForNull_IsNull()
    {
        Assert.True(Convert(RedisResult.Create(RedisValue.Null)).IsNull);
    }

    /// <summary>
    /// 字符串返回值可取回字符串
    /// </summary>
    [Fact]
    public void ToScriptResult_ForString_ReturnsString()
    {
        Assert.Equal("ok", Convert(RedisResult.Create((RedisValue)"ok")).AsString());
    }

    /// <summary>
    /// 整数返回值可取回整数与布尔
    /// </summary>
    [Fact]
    public void ToScriptResult_ForInteger_ReturnsInt64AndBoolean()
    {
        var result = Convert(RedisResult.Create((RedisValue)1L));

        Assert.Equal(1L, result.AsInt64());
        Assert.True(result.AsBoolean());
    }

    /// <summary>
    /// 零值按假判定
    /// </summary>
    [Fact]
    public void ToScriptResult_ForZero_IsFalse()
    {
        Assert.False(Convert(RedisResult.Create((RedisValue)0L)).AsBoolean());
    }

    /// <summary>
    /// 数组返回值可逐项取回
    /// </summary>
    [Fact]
    public void ToScriptResult_ForArray_ReturnsItems()
    {
        var source = RedisResult.Create([RedisResult.Create((RedisValue)"a"), RedisResult.Create((RedisValue)"b")]);

        var result = Convert(source);

        Assert.True(result.IsArray);
        Assert.Equal(["a", "b"], result.AsArray().Select(item => item.AsString()));
    }

    /// <summary>
    /// 嵌套数组逐层转换
    /// </summary>
    [Fact]
    public void ToScriptResult_ForNestedArray_ConvertsRecursively()
    {
        var inner = RedisResult.Create([RedisResult.Create((RedisValue)"x")]);
        var source = RedisResult.Create([inner]);

        var result = Convert(source);

        Assert.Equal("x", result.AsArray()[0].AsArray()[0].AsString());
    }

    /// <summary>
    /// 空结果取值返回空
    /// </summary>
    [Fact]
    public void NullResult_AccessorsReturnNull()
    {
        Assert.Null(CacheScriptResult.Null.AsString());
        Assert.Null(CacheScriptResult.Null.AsInt64());
        Assert.Null(CacheScriptResult.Null.AsBoolean());
        Assert.Empty(CacheScriptResult.Null.AsArray());
    }

    /// <summary>
    /// 非数组结果取数组返回空集合
    /// </summary>
    [Fact]
    public void ScalarResult_AsArrayIsEmpty()
    {
        Assert.Empty(CacheScriptResult.FromValue("scalar").AsArray());
    }

    /// <summary>
    /// 判断方法签名是否引用了 Redis 类型
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns>是否引用</returns>
    private static bool ReferencesRedisType(MethodInfo method)
    {
        return IsRedisType(method.ReturnType) || method.GetParameters().Any(parameter => IsRedisType(parameter.ParameterType));
    }

    /// <summary>
    /// 判断类型是否来自 Redis 客户端
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否来自 Redis 客户端</returns>
    private static bool IsRedisType(Type type)
    {
        if (type.Assembly.GetName().Name?.StartsWith("StackExchange.Redis", StringComparison.Ordinal) == true)
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(IsRedisType);
    }

    /// <summary>
    /// 经 Redis 缓存实现执行转换
    /// </summary>
    /// <param name="result">Redis 返回值</param>
    /// <returns>中立结果</returns>
    private static CacheScriptResult Convert(RedisResult result)
    {
        var method = typeof(XiHanRedisCache).GetMethod(
            "ToScriptResult",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        return (CacheScriptResult)method.Invoke(null, [result])!;
    }
}
