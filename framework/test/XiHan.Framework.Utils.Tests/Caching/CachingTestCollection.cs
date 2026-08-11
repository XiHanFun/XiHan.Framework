// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Tests.Caching;

/// <summary>
/// 缓存测试集合
/// </summary>
/// <remarks>
/// <see cref="XiHan.Framework.Utils.Caching.CacheHelper"/> 是静态类，缓存字典与
/// 淘汰策略、容量上限等配置均为进程级共享。各测试类会 Clear 并重新 Configure，
/// 若并行执行则彼此的缓存项与配置互相污染，容量与淘汰断言随机失败。
/// 归入同一集合以保证类间串行。
/// </remarks>
[CollectionDefinition(Name)]
public class CachingTestCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "Caching";
}
