# 缓存管理工具

## 简介

缓存管理工具提供了简单高效的内存缓存能力，支持键值对存储、过期策略和自动清理，适用于需要高性能数据访问的场景。

## 主要组件

### MemoryCache 类

提供了基本的内存缓存功能，支持设置过期时间、自动清理过期项等功能。

### CacheManager 类

单例类，提供了对多个缓存实例的统一管理，允许创建命名缓存实例，实现缓存隔离。

## 功能特点

- **高性能**: 基于`ConcurrentDictionary`实现，支持高并发访问
- **类型安全**: 支持泛型，保证类型安全
- **过期策略**: 支持绝对过期时间和相对过期时间
- **自动清理**: 定时清理过期项，避免内存泄漏
- **缓存隔离**: 支持创建多个命名缓存实例，实现数据隔离
- **异步支持**: 提供异步方法，适合 IO 密集型场景

## 使用示例

### 基本使用

```csharp
// 获取默认缓存实例
var cache = CacheManager.Instance.DefaultCache;

// 设置缓存项（无过期时间）
cache.Set("key1", "value1");

// 设置缓存项（绝对过期时间）
cache.Set("key2", "value2", DateTime.Now.AddMinutes(10));

// 设置缓存项（相对过期时间）
cache.Set("key3", "value3", TimeSpan.FromMinutes(5));

// 获取缓存项
if (cache.TryGet("key1", out string value))
{
    Console.WriteLine($"Value: {value}");
}

// 删除缓存项
cache.Remove("key1");

// 清空缓存
cache.Clear();
```

### 获取或添加模式

```csharp
// 获取缓存值，如果不存在则计算并缓存
var result = cache.GetOrAdd("expensiveOperation", () =>
{
    // 执行昂贵的计算
    return CalculateExpensiveValue();
}, TimeSpan.FromMinutes(10));

// 异步版本
var result = await cache.GetOrAddAsync("expensiveAsyncOperation", async () =>
{
    // 执行昂贵的异步计算
    return await CalculateExpensiveValueAsync();
}, TimeSpan.FromMinutes(10));
```

### 多缓存管理

```csharp
// 获取命名缓存实例
var userCache = CacheManager.Instance.GetCache("UserCache");
var productCache = CacheManager.Instance.GetCache("ProductCache");

// 在不同缓存中存储数据
userCache.Set("user1", new User { Id = 1, Name = "张三" });
productCache.Set("product1", new Product { Id = 101, Name = "示例产品" });

// 清空所有缓存
CacheManager.Instance.ClearAllCaches();

// 获取所有缓存名称
var cacheNames = CacheManager.Instance.GetCacheNames();
```

## 最佳实践

1. **合理设置过期时间**: 根据数据的变化频率设置合适的过期时间
2. **避免存储大对象**: 内存缓存适合存储小到中等大小的对象
3. **使用缓存隔离**: 为不同类型的数据创建不同的缓存实例
4. **缓存常用数据**: 优先缓存频繁访问且计算成本高的数据
5. **定期监控缓存大小**: 避免缓存过度增长导致内存压力

## 限制说明

- 仅支持内存缓存，重启后数据会丢失
- 不支持分布式场景，多实例间缓存不共享
- 不支持缓存穿透、缓存击穿、缓存雪崩的高级防护策略
