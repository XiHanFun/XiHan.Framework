// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Interceptors;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 缓存切面对 ValueTask 返回形态的解析测试
/// </summary>
/// <remarks>
/// 值类型解析只对 Task&lt;T&gt; 做过解包，ValueTask&lt;T&gt; 会落到「返回类型即值类型」的兜底分支，
/// 把未展开的 ValueTask 结构本身当缓存值序列化下来，命中时还回去的是反序列化出的空句柄而不是结果。
/// 也不能改成取 ValueTask&lt;T&gt; 的类型实参：动态代理适配器只按 void / Task / Task&lt;T&gt; 分派，
/// ValueTask&lt;T&gt; 方法走同步分支，把裸 T 写回返回值槽位会在返回时当场类型转换失败。
/// 因此正确口径是判为不可缓存，让这类方法退回直连执行。
/// </remarks>
public class CacheAspectValueTaskTests
{
    /// <summary>
    /// 返回 ValueTask 形态的方法一律判为不可缓存
    /// </summary>
    [Theory]
    [InlineData(nameof(Target.GetNameValueTaskAsync))]
    [InlineData(nameof(Target.GetCountValueTaskAsync))]
    [InlineData(nameof(Target.DoNothingValueTaskAsync))]
    public void GetCacheableValueTypeOrNull_ForValueTaskLikeMethod_ReturnsNull(string methodName)
    {
        Assert.Null(CacheAspect.GetCacheableValueTypeOrNull(GetMethod(methodName)));
    }

    /// <summary>
    /// Task 形态的解包口径不受影响
    /// </summary>
    /// <remarks>
    /// 反例：确认为 ValueTask 加的判定没有把 Task&lt;T&gt; 一起挡掉。
    /// </remarks>
    [Fact]
    public void GetCacheableValueTypeOrNull_ForGenericTask_StillReturnsTypeArgument()
    {
        Assert.Equal(typeof(string), CacheAspect.GetCacheableValueTypeOrNull(GetMethod(nameof(Target.GetNameTaskAsync))));
    }

    /// <summary>
    /// 同步方法的返回类型仍原样作为可缓存值类型
    /// </summary>
    [Fact]
    public void GetCacheableValueTypeOrNull_ForSyncMethod_StillReturnsReturnType()
    {
        Assert.Equal(typeof(int), CacheAspect.GetCacheableValueTypeOrNull(GetMethod(nameof(Target.GetCountSync))));
    }

    /// <summary>
    /// 取目标类型上的方法
    /// </summary>
    /// <param name="name">方法名</param>
    /// <returns>方法信息</returns>
    private static MethodInfo GetMethod(string name)
    {
        return typeof(Target).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
    }

    /// <summary>
    /// 承载各种返回形态的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 返回引用类型 ValueTask 的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        [Cacheable(Key = "vt:name:{id}", ExpireSeconds = 60)]
        public ValueTask<string> GetNameValueTaskAsync(int id)
        {
            return ValueTask.FromResult($"n{id}");
        }

        /// <summary>
        /// 返回值类型 ValueTask 的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>数量</returns>
        [Cacheable(Key = "vt:count:{id}", ExpireSeconds = 60)]
        public ValueTask<int> GetCountValueTaskAsync(int id)
        {
            return ValueTask.FromResult(id);
        }

        /// <summary>
        /// 返回无结果 ValueTask 的方法
        /// </summary>
        /// <returns>异步任务</returns>
        public ValueTask DoNothingValueTaskAsync()
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 返回 Task 的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        [Cacheable(Key = "task:name:{id}", ExpireSeconds = 60)]
        public Task<string> GetNameTaskAsync(int id)
        {
            return Task.FromResult($"n{id}");
        }

        /// <summary>
        /// 同步返回值的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>数量</returns>
        [Cacheable(Key = "sync:count:{id}")]
        public int GetCountSync(int id)
        {
            return id;
        }
    }
}
