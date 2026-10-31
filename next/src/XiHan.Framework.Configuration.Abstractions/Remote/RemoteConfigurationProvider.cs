// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置的 Provider。
/// </summary>
/// <remarks>
/// 首次同步加载，之后按周期带已知版本轮询；拿到新版本就整体替换并触发重载，失败保留最后一份成功的数据。
/// </remarks>
internal sealed class RemoteConfigurationProvider : KeyValueConfigurationProvider
{
    /// <summary>
    /// 客户端。
    /// </summary>
    private readonly IRemoteConfigurationClient _client;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly RemoteConfigurationOptions _options;

    /// <summary>
    /// 停止轮询的信号。
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 轮询定时器。
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// 轮询循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 最后一次成功加载的版本。
    /// </summary>
    private string? _version;

    /// <summary>
    /// 创建。
    /// </summary>
    public RemoteConfigurationProvider(IRemoteConfigurationClient client, RemoteConfigurationOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public override void Load()
    {
        // IConfigurationProvider.Load 是同步契约：在线程池上执行异步取值并等待，受 LoadTimeout 约束
        using var timeout = new CancellationTokenSource(_options.LoadTimeout);
        Result<Maybe<ConfigurationDocument>> fetched;
        try
        {
            fetched = Task.Run(() => _client.FetchAsync(_options.Namespace, null, timeout.Token).AsTask(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            fetched = Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", _options.Namespace), ("reason", "load timeout")));
        }

        if (!fetched.TryGetValue(out var maybe))
        {
            _options.OnError?.Invoke(fetched.Error!);
            if (!_options.Optional)
            {
                throw new FrameworkException(fetched.Error!.Code, $"Remote configuration namespace '{_options.Namespace}' could not be loaded ({fetched.Error.Code}).");
            }
        }
        else if (maybe.TryGetValue(out var document))
        {
            Apply(document, notify: false);
        }

        if (_options.PollInterval is { } interval && _loop is null)
        {
            _timer = new PeriodicTimer(interval, _options.Clock);
            var (timer, stopping) = (_timer, _stopping.Token);
            _loop = Task.Run(() => PollAsync(timer, stopping), CancellationToken.None);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stopping.Cancel();
            _timer?.Dispose();
            _stopping.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 把文档按前缀挂进键值表。
    /// </summary>
    private void Apply(ConfigurationDocument document, bool notify)
    {
        var values = document.Values;
        if (_options.Prefix is { } prefix)
        {
            var prefixed = new Dictionary<string, string?>(values.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values)
            {
                prefixed[ConfigurationPath.Combine(prefix, key)] = value;
            }

            values = prefixed;
        }

        _version = document.Version;
        Replace(values, notify);
    }

    /// <summary>
    /// 轮询。
    /// </summary>
    /// <remarks>
    /// 带已知版本取值，拿到新文档就替换并触发重载；失败只回调，不丢弃已有数据。
    /// </remarks>
    private async Task PollAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Result<Maybe<ConfigurationDocument>> fetched;
                try
                {
                    fetched = await _client.FetchAsync(_options.Namespace, _version, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // 客户端按契约应返回失败结果；抛出的异常同样只报告，保留最后一份成功的数据
                    fetched = Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", _options.Namespace), ("reason", exception.Message)));
                }

                if (!fetched.TryGetValue(out var maybe))
                {
                    _options.OnError?.Invoke(fetched.Error!);
                }
                else if (maybe.TryGetValue(out var document) && document.Version != _version)
                {
                    Apply(document, notify: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
    }
}
