// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 幂等契约模型测试
/// </summary>
public class IdempotencyContractTests
{
    /// <summary>
    /// 相同字段的记录键哈希稳定
    /// </summary>
    [Fact]
    public void ComputeHash_SameFields_ReturnsSameHash()
    {
        var first = new IdempotencyRecordKey("1", "42", "POST", "/api/orders", "key-1");
        var second = new IdempotencyRecordKey("1", "42", "POST", "/api/orders", "key-1");

        Assert.Equal(first.ComputeHash(), second.ComputeHash());
        Assert.Equal(64, first.ComputeHash().Length);
    }

    /// <summary>
    /// 租户、主体、方法、端点、键任一不同则哈希不同
    /// </summary>
    [Theory]
    [InlineData("2", "42", "POST", "/api/orders", "key-1")]
    [InlineData("1", "43", "POST", "/api/orders", "key-1")]
    [InlineData("1", "42", "PUT", "/api/orders", "key-1")]
    [InlineData("1", "42", "POST", "/api/order", "key-1")]
    [InlineData("1", "42", "POST", "/api/orders", "key-2")]
    public void ComputeHash_AnyFieldDiffers_ReturnsDifferentHash(string tenantId, string subjectId, string method, string endpoint, string key)
    {
        var baseline = new IdempotencyRecordKey("1", "42", "POST", "/api/orders", "key-1");
        var other = new IdempotencyRecordKey(tenantId, subjectId, method, endpoint, key);

        Assert.NotEqual(baseline.ComputeHash(), other.ComputeHash());
    }

    /// <summary>
    /// 字段边界移动不会产生相同哈希
    /// </summary>
    [Fact]
    public void ComputeHash_FieldBoundaryShift_ReturnsDifferentHash()
    {
        var first = new IdempotencyRecordKey("1", "ab", "POST", "/x", "c");
        var second = new IdempotencyRecordKey("1", "a", "POST", "/x", "bc");

        Assert.NotEqual(first.ComputeHash(), second.ComputeHash());
    }

    /// <summary>
    /// 键校验：非空、不超长、仅可见 ASCII
    /// </summary>
    [Theory]
    [InlineData("order-2026-0001", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("has space", false)]
    [InlineData("中文键", false)]
    public void IsValid_ChecksCharacters(string key, bool expected)
    {
        Assert.Equal(expected, IdempotencyKeyValidator.IsValid(key, 128));
    }

    /// <summary>
    /// 键超过长度上限时无效
    /// </summary>
    [Fact]
    public void IsValid_TooLong_ReturnsFalse()
    {
        Assert.True(IdempotencyKeyValidator.IsValid(new string('a', 128), 128));
        Assert.False(IdempotencyKeyValidator.IsValid(new string('a', 129), 128));
        Assert.False(IdempotencyKeyValidator.IsValid(null, 128));
    }

    /// <summary>
    /// 默认选项与规格一致
    /// </summary>
    [Fact]
    public void Options_Defaults_MatchSpec()
    {
        var options = new XiHanIdempotencyOptions();

        Assert.Equal("XiHan:Web:Api:Idempotency", XiHanIdempotencyOptions.SectionName);
        Assert.Equal("Idempotency-Key", options.HeaderName);
        Assert.Equal(128, options.MaxKeyLength);
        Assert.Equal(1024 * 1024, options.MaxRequestBytes);
        Assert.Equal(1024 * 1024, options.MaxResponseBytes);
        Assert.Equal(10_000, options.MaxEntries);
        Assert.Equal(64L * 1024 * 1024, options.MaxTotalResponseBytes);
        Assert.Equal(TimeSpan.FromHours(24), options.CompletedRetention);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ProcessingLease);
    }

    /// <summary>
    /// 取得结果的工厂方法设置对应状态
    /// </summary>
    [Fact]
    public void AcquireResult_Factories_SetStatus()
    {
        var token = Guid.NewGuid();
        var response = new StoredResponse(201, [1, 2]);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, IdempotencyAcquireResult.Acquired(token).Status);
        Assert.Equal(token, IdempotencyAcquireResult.Acquired(token).OwnerToken);
        Assert.Same(response, IdempotencyAcquireResult.Replay(response).Response);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, IdempotencyAcquireResult.InProgress().Status);
        Assert.Equal(IdempotencyAcquireStatus.Conflict, IdempotencyAcquireResult.Conflict().Status);
        Assert.Equal(IdempotencyAcquireStatus.Indeterminate, IdempotencyAcquireResult.Indeterminate().Status);
        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, IdempotencyAcquireResult.CapacityExceeded().Status);
    }
}
