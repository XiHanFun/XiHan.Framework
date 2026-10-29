// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 密钥配置的 Provider。
/// </summary>
/// <remarks>
/// 构建时一次批量读取映射的密钥，可按周期重读；重读失败的项保留旧值。
/// </remarks>
internal sealed class SecretsConfigurationProvider : KeyValueConfigurationProvider
{
    /// <summary>
    /// 密钥库。
    /// </summary>
    private readonly ISecretStore _store;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly SecretsConfigurationOptions _options;

    /// <summary>
    /// 映射的配置键，顺序固定，与批量请求的名字一一对应。
    /// </summary>
    private readonly string[] _keys;

    /// <summary>
    /// 映射的密钥名，与 <see cref="_keys"/> 同序。
    /// </summary>
    private readonly SecretName[] _names;

    /// <summary>
    /// 停止重读的信号。
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 重读定时器。
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// 重读循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建。
    /// </summary>
    public SecretsConfigurationProvider(ISecretStore store, SecretsConfigurationOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _keys = [.. options.Mappings.Keys];
        _names = [.. options.Mappings.Values];
    }

    /// <inheritdoc />
    public override void Load()
    {
        // IConfigurationProvider.Load 是同步契约：在线程池上执行批量读取并等待，受 LoadTimeout 约束
        using var timeout = new CancellationTokenSource(_options.LoadTimeout);
        IReadOnlyList<SecretLookup> lookups;
        try
        {
            lookups = Task.Run(() => _store.GetAsync(_names, timeout.Token).AsTask(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            var error = Error.Unavailable(ConfigurationErrors.SecretsUnavailable, ConfigurationMessages.SecretsUnavailable, MessageArguments.Of(("reason", "load timeout")));
            _options.OnError?.Invoke(error);
            if (_options.Optional)
            {
                return;
            }

            throw new FrameworkException(error.Code, $"Secrets for configuration keys '{string.Join("', '", _keys)}' could not be loaded ({error.Code}).");
        }

        Apply(lookups, initial: true);
        if (_options.RefreshInterval is { } interval && _loop is null)
        {
            _timer = new PeriodicTimer(interval);
            var (timer, stopping) = (_timer, _stopping.Token);
            _loop = Task.Run(() => RefreshAsync(timer, stopping), CancellationToken.None);
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
    /// 把逐项结果写进键值表。
    /// </summary>
    /// <remarks>
    /// 首次时失败项按 <see cref="SecretsConfigurationOptions.Optional"/> 决定跳过或抛出；重读时失败项保留旧值。
    /// </remarks>
    private void Apply(IReadOnlyList<SecretLookup> lookups, bool initial)
    {
        if (lookups.Count != _names.Length)
        {
            throw new FrameworkException(ConfigurationErrors.SecretsUnavailable, $"The secret store returned {lookups.Count} results for {_names.Length} names.");
        }

        var data = initial ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) : Snapshot();
        List<string>? failed = null;
        string? firstCode = null;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (lookups[i].Result.TryGetValue(out var secret))
            {
                data[_keys[i]] = secret.Value;
                continue;
            }

            var error = lookups[i].Result.Error!;
            _options.OnError?.Invoke(error);
            if (initial && !_options.Optional)
            {
                (failed ??= []).Add(_keys[i]);
                firstCode ??= error.Code;
            }
        }

        if (failed is not null)
        {
            throw new FrameworkException(firstCode!, $"Secrets for configuration keys '{string.Join("', '", failed)}' could not be loaded ({firstCode}).");
        }

        Replace(data, notify: !initial);
    }

    /// <summary>
    /// 周期性重读。
    /// </summary>
    /// <remarks>
    /// 失败只回调，不丢弃已有数据。
    /// </remarks>
    private async Task RefreshAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                IReadOnlyList<SecretLookup> lookups;
                try
                {
                    lookups = await _store.GetAsync(_names, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // 密钥库按契约应逐项返回失败；整体抛出时同样只报告一次，保留旧值
                    _options.OnError?.Invoke(Error.Unavailable(ConfigurationErrors.SecretsUnavailable, ConfigurationMessages.SecretsUnavailable, MessageArguments.Of(("reason", exception.Message))));
                    continue;
                }

                if (lookups.Count == _names.Length)
                {
                    Apply(lookups, initial: false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停止
        }
    }
}
