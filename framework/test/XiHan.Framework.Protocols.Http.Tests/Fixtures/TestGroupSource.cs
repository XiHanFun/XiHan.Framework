// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Primitives;
using XiHan.Framework.Protocols.Http.OpenApi;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 由测试改写的运行期分组来源
/// </summary>
/// <remarks>
/// <see cref="Set"/> 换上新的分组并触发令牌，模拟应用在插件加载与卸载时增减分组
/// </remarks>
internal sealed class TestGroupSource : IOpenApiGroupSource, IDisposable
{
    /// <summary>
    /// 分组与令牌的互斥锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前的分组
    /// </summary>
    private IReadOnlyList<OpenApiGroup> _groups = [];

    /// <summary>
    /// 当前这一代令牌的取消源
    /// </summary>
    private CancellationTokenSource _changes = new();

    /// <inheritdoc />
    public IReadOnlyList<OpenApiGroup> Groups
    {
        get
        {
            lock (_gate)
            {
                return _groups;
            }
        }
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        lock (_gate)
        {
            return new CancellationChangeToken(_changes.Token);
        }
    }

    /// <summary>
    /// 换上新的分组并触发令牌
    /// </summary>
    public void Set(IReadOnlyList<OpenApiGroup> groups)
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            _groups = groups;
            previous = _changes;
            _changes = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => _changes.Dispose();
}
