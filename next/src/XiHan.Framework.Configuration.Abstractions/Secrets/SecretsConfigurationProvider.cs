// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Secrets;

/// <summary>
/// 密钥配置的 Provider。
/// </summary>
/// <remarks>
/// 构建时一次批量读取映射的密钥，可按周期重读；重读失败的项保留旧值。整体失败（超时、密钥库抛出、条数与请求不一致）在首次读取与重读时一样回调 <see cref="SecretsConfigurationOptions.OnError"/>，首次失败再按 <see cref="SecretsConfigurationOptions.Optional"/> 抛出或为空，不论成败都开始重读。
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
        IReadOnlyList<SecretLookup>? lookups = null;
        Exception? thrown = null;
        string? reason;
        try
        {
            lookups = Task.Run(() => _store.GetAsync(_names, timeout.Token).AsTask(), CancellationToken.None).GetAwaiter().GetResult();
            reason = lookups.Count == _names.Length ? null : Miscounted(lookups.Count);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            reason = "load timeout";
        }
        catch (Exception exception)
        {
            // 密钥库按契约应逐项返回失败；整体抛出的异常与超时一样走 OnError 与 Optional，而不是绕过它们直接让构建失败
            (thrown, reason) = (exception, exception.Message);
        }

        if (reason is not null)
        {
            var error = Unavailable(reason);
            Report(error);
            if (!_options.Optional)
            {
                throw new FrameworkException(error.Code, $"Secrets for configuration keys '{string.Join("', '", _keys)}' could not be loaded ({error.Code}: {reason}).", thrown);
            }
        }
        else
        {
            Apply(lookups!, initial: true);
        }

        // 可选的首次失败之后照常重读：失败的项此时为空，之后第一次读到即装入
        if (_options.RefreshInterval is { } interval && _loop is null)
        {
            _timer = new PeriodicTimer(interval, _options.Clock);
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
    /// 调用方已核对条数。首次时失败项按 <see cref="SecretsConfigurationOptions.Optional"/> 决定跳过或抛出；重读时失败项保留旧值。
    /// </remarks>
    private void Apply(IReadOnlyList<SecretLookup> lookups, bool initial)
    {
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
            Report(error);
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
    /// 条数与请求不一致时的原因。
    /// </summary>
    private string Miscounted(int count)
        => string.Create(CultureInfo.InvariantCulture, $"the secret store returned {count} results for {_names.Length} names");

    /// <summary>
    /// 密钥库整体不可达的失败，原因写进参数。
    /// </summary>
    private static Error Unavailable(string reason)
        => Error.Unavailable(ConfigurationErrors.SecretsUnavailable, ConfigurationMessages.SecretsUnavailable, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 回调 <see cref="SecretsConfigurationOptions.OnError"/>。
    /// </summary>
    /// <remarks>
    /// 回调是唯一的报告渠道，它自己抛出的异常没有更上一层可报告，丢弃：不改变首次读取的结果，也不终止重读。
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
    /// 周期性重读。
    /// </summary>
    /// <remarks>
    /// 失败只回调，不丢弃已有数据：逐项失败的项保留旧值，整体失败（密钥库抛出、条数不符）保留全部旧值。停止之后的任何异常都不再报告。
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
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // 密钥库按契约应逐项返回失败；整体抛出时同样只报告一次，保留旧值
                    Report(Unavailable(exception.Message));
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (lookups.Count != _names.Length)
                {
                    // 条数不符时逐项对不上配置键：整体报告一次，保留旧值
                    Report(Unavailable(Miscounted(lookups.Count)));
                    continue;
                }

                Apply(lookups, initial: false);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // 停止：等待被取消，或释放与最后一次写回交错
        }
    }
}
