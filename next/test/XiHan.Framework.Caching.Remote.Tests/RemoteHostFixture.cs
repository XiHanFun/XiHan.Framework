// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 一个测试类共用的宿主。
/// </summary>
/// <remarks>
/// 构建并加载 cache-go 一次，各用例通过 <see cref="Store"/> 取远程存储；没有 go 时让用例跳过。
/// </remarks>
public sealed class RemoteHostFixture : IAsyncLifetime
{
    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 跳过时为空。
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 跳过原因。
    /// </summary>
    /// <remarks>
    /// 为空表示宿主可用。
    /// </remarks>
    private string? _skipReason;

    /// <summary>
    /// 远程存储。
    /// </summary>
    /// <remarks>
    /// 本机没有 go 时跳过当前用例。
    /// </remarks>
    public RemoteCacheStore Store
    {
        get
        {
            if (_skipReason is { } reason)
            {
                Assert.Skip(reason);
            }

            return _host!.Services.GetRequiredService<RemoteCacheStore>();
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _skipReason = CacheGoPluginHost.BuildReason;
        if (_skipReason is null)
        {
            _host = await CacheGoPluginHost.StartAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}
