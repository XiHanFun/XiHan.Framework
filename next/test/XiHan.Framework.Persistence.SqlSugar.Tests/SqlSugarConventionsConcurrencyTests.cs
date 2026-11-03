// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 并发首次建实体映射时约定的线程安全
/// </summary>
/// <remarks>
/// 多个客户端会并发地首次建实体映射（并行的请求、并行的测试），约定判定可空引用列用的上下文是共用的；
/// 不加锁时它的内部字典被并发写坏，建映射抛出“已添加了具有相同键的项”，启动或第一次查询随之失败
/// </remarks>
public sealed class SqlSugarConventionsConcurrencyTests
{
    [Fact]
    public void Configure_FromManyThreads_OnPropertiesNotMappedBefore_GivesTheSingleThreadedAnswer()
    {
        // 取几个框架程序集里可写的字符串属性：每个属性第一次判定可空性都要写进上下文的缓存，数量足够多才可能撞上并发写入
        var properties = new[] { typeof(App), typeof(Error), typeof(EntityIndex), typeof(SqlSugarConventions) }
            .Select(static type => type.Assembly)
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Namespace?.StartsWith("XiHan.", StringComparison.Ordinal) == true)
            .SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static property => property.PropertyType == typeof(string) && property.SetMethod is { IsPublic: true })
            .ToArray();
        Assert.True(properties.Length > 50, $"Expected enough properties to race on, found {properties.Length}.");

        var columns = new EntityColumnInfo[properties.Length];
        Parallel.For(0, properties.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) }, index =>
        {
            columns[index] = new EntityColumnInfo();
            SqlSugarConventions.Configure(properties[index], columns[index]);
        });

        // 并发的结果与单线程逐个判定的一致：可空引用（string?）列可空，其余不可空
        var expected = new NullabilityInfoContext();
        for (var index = 0; index < properties.Length; index++)
        {
            Assert.Equal(expected.Create(properties[index]).WriteState == NullabilityState.Nullable, columns[index].IsNullable);
        }
    }
}
