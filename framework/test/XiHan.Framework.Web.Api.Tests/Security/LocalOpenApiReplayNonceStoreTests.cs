// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Api.Security.OpenApi;

namespace XiHan.Framework.Web.Api.Tests.Security;

public sealed class LocalOpenApiReplayNonceStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_ConcurrentSameNonce_OnlyOneRequestAcquires()
    {
        var store = new LocalOpenApiReplayNonceStore();
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            store.TryAcquireAsync("client", "nonce", DateTimeOffset.UtcNow.AddMinutes(5), 100)));

        Assert.Equal(1, results.Count(result => result == OpenApiNonceAcquisitionResult.Acquired));
        Assert.Equal(31, results.Count(result => result == OpenApiNonceAcquisitionResult.AlreadyUsed));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAtCapacity_RejectsNewNonce()
    {
        var store = new LocalOpenApiReplayNonceStore();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        Assert.Equal(
            OpenApiNonceAcquisitionResult.Acquired,
            await store.TryAcquireAsync("client", "first", expiresAt, 1));
        Assert.Equal(
            OpenApiNonceAcquisitionResult.CapacityExceeded,
            await store.TryAcquireAsync("client", "second", expiresAt, 1));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAtCapacity_RemovesExpiredEntriesBeforeRejecting()
    {
        var store = new LocalOpenApiReplayNonceStore();

        Assert.Equal(
            OpenApiNonceAcquisitionResult.Acquired,
            await store.TryAcquireAsync("client", "expired", DateTimeOffset.UtcNow.AddSeconds(-1), 1));
        Assert.Equal(
            OpenApiNonceAcquisitionResult.Acquired,
            await store.TryAcquireAsync("client", "current", DateTimeOffset.UtcNow.AddMinutes(5), 1));
    }
}
