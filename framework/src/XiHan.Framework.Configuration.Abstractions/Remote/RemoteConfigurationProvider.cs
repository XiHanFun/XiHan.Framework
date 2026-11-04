// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置的 Provider
/// </summary>
/// <remarks>
/// 首次同步加载，之后按周期带已知版本轮询；拿到新版本就整体替换并触发重载，失败保留最后一份成功的数据。首次加载与轮询的失败（返回失败、超时、客户端抛出）一样回调 <see cref="RemoteConfigurationOptions.OnError"/>，首次失败再按 <see cref="RemoteConfigurationOptions.Optional"/> 抛出或为空，不论成败都开始轮询
/// </remarks>
internal sealed class RemoteConfigurationProvider : KeyValueConfigurationProvider
{
    /// <summary>
    /// 客户端
    /// </summary>
    private readonly IRemoteConfigurationClient _client;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly RemoteConfigurationOptions _options;

    /// <summary>
    /// 客户端是否归本 Provider 所有（释放时一并释放）
    /// </summary>
    private readonly bool _ownsClient;

    /// <summary>
    /// 停止轮询的信号
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 轮询定时器
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// 轮询循环
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 最后一次成功加载的版本
    /// </summary>
    private string? _version;

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="ownsClient"/> 为真时客户端归本 Provider 所有，释放时一并释放（实现 <see cref="IDisposable"/> 时）
    /// </remarks>
    public RemoteConfigurationProvider(IRemoteConfigurationClient client, RemoteConfigurationOptions options, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = ownsClient;
    }

    /// <inheritdoc />
    public override void Load()
    {
        // IConfigurationProvider.Load 是同步契约：在线程池上执行异步取值并等待，受 LoadTimeout 约束
        using var timeout = new CancellationTokenSource(_options.LoadTimeout);
        Result<Maybe<ConfigurationDocument>> fetched;
        Exception? thrown = null;
        try
        {
            fetched = Task.Run(() => _client.FetchAsync(_options.Namespace, null, timeout.Token).AsTask(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            fetched = Unavailable("load timeout");
        }
        catch (Exception exception)
        {
            // 客户端按契约应返回失败结果；抛出的异常与返回失败一样走 OnError 与 Optional，而不是绕过它们直接让构建失败
            thrown = exception;
            fetched = Unavailable(exception.Message);
        }

        if (!fetched.TryGetValue(out var maybe))
        {
            var error = fetched.Error!;
            Report(error);
            if (!_options.Optional)
            {
                throw new FrameworkException(error.Code, $"Remote configuration namespace '{_options.Namespace}' could not be loaded ({error.Code}).", thrown);
            }
        }
        else if (maybe.TryGetValue(out var document))
        {
            Apply(document, notify: false);
        }

        // 可选的首次失败之后照常轮询：已知版本为空，第一次成功的轮询装入整份文档
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
            // 先停轮询再释放客户端：停止之后循环里因客户端已释放而出的异常不会被报告
            _stopping.Cancel();
            _timer?.Dispose();
            if (_ownsClient && _client is IDisposable client)
            {
                client.Dispose();
            }

            _stopping.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 把文档按前缀挂进键值表
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
    /// 不可达的失败，原因写进参数
    /// </summary>
    private Error Unavailable(string reason)
        => Error.Unavailable(ConfigurationErrors.RemoteUnavailable, ConfigurationMessages.RemoteUnavailable, MessageArguments.Of(("namespace", _options.Namespace), ("reason", reason)));

    /// <summary>
    /// 回调 <see cref="RemoteConfigurationOptions.OnError"/>
    /// </summary>
    /// <remarks>
    /// 回调是唯一的报告渠道，它自己抛出的异常没有更上一层可报告，丢弃：不改变首次加载的结果，也不终止轮询
    /// </remarks>
    private void Report(Error error)
    {
        try
        {
            _options.OnError?.Invoke(error);
        }
        catch (Exception)
        {
            // 丢弃：见 remarks
        }
    }

    /// <summary>
    /// 轮询
    /// </summary>
    /// <remarks>
    /// 带已知版本取值，拿到新文档就替换并触发重载；失败只回调，不丢弃已有数据。停止之后的任何异常都不再报告
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
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // 客户端按契约应返回失败结果；抛出的异常同样只报告，保留最后一份成功的数据
                    fetched = Unavailable(exception.Message);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!fetched.TryGetValue(out var maybe))
                {
                    Report(fetched.Error!);
                }
                else if (maybe.TryGetValue(out var document) && document.Version != _version)
                {
                    Apply(document, notify: true);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 停止：等待被取消，或释放与最后一次写回交错
        }
    }
}
