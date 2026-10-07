// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// 单进程内有容量上限的 OpenApi nonce 存储。
/// </summary>
/// <remarks>仅适用于单实例；多实例部署应替换为具备跨实例原子认领能力的实现。</remarks>
public sealed class LocalOpenApiReplayNonceStore : IOpenApiReplayNonceStore
{
    private readonly object _sync = new();
    private readonly Dictionary<NonceKey, DateTimeOffset> _entries = [];
    private int _cleanupCounter;

    /// <inheritdoc />
    public Task<OpenApiNonceAcquisitionResult> TryAcquireAsync(
        string accessKey,
        string nonce,
        DateTimeOffset expiresAt,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);

        var key = new NonceKey(accessKey, nonce);
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_entries.TryGetValue(key, out var existingExpiry))
            {
                if (existingExpiry > now)
                {
                    return Task.FromResult(OpenApiNonceAcquisitionResult.AlreadyUsed);
                }

                _entries.Remove(key);
            }

            if (_entries.Count >= maxEntries)
            {
                RemoveExpired(now);
                if (_entries.Count >= maxEntries)
                {
                    return Task.FromResult(OpenApiNonceAcquisitionResult.CapacityExceeded);
                }
            }

            _entries.Add(key, expiresAt);
            if (++_cleanupCounter % 256 == 0)
            {
                RemoveExpired(now);
            }

            return Task.FromResult(OpenApiNonceAcquisitionResult.Acquired);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        var expiredKeys = _entries
            .Where(entry => entry.Value <= now)
            .Select(entry => entry.Key)
            .ToArray();
        foreach (var key in expiredKeys)
        {
            _entries.Remove(key);
        }
    }

    private readonly record struct NonceKey(string AccessKey, string Nonce);
}
