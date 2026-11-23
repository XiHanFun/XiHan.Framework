// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 插件目录里的清单：读取与种类核对
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（ADR-0260）。两个宿主只差要求的隔离方式，以及隔离方式不符时各自的消息键
/// </remarks>
internal static class PluginManifests
{
    /// <summary>
    /// 读目录里的清单并确认是指定隔离方式的插件
    /// </summary>
    /// <param name="directory">插件目录</param>
    /// <param name="isolation">宿主承载的隔离方式</param>
    public static Result<Manifest> Read(string directory, PluginIsolation isolation)
    {
        var read = ReadFile(directory);
        if (!read.TryGetValue(out var manifest))
        {
            return read.Error!;
        }

        var kind = CheckKind(manifest, isolation);
        return kind.IsSuccess ? manifest : kind.Error!;
    }

    /// <summary>
    /// 读目录里的清单文件
    /// </summary>
    /// <remarks>
    /// 读不出来时返回 <c>PLUGINS_VALIDATION_001</c>，清单读取器的错误作为原因带在里面
    /// </remarks>
    /// <param name="directory">插件目录</param>
    public static Result<Manifest> ReadFile(string directory)
    {
        var read = ManifestReader.ReadFile(Path.Combine(directory, ManifestReader.FileName));
        return read.TryGetValue(out var manifest)
            ? manifest
            : new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", directory), ("reason", read.Error!.Message)), read.Error);
    }

    /// <summary>
    /// 清单是否描述一个指定隔离方式的插件
    /// </summary>
    /// <remarks>
    /// 不是时返回 <c>PLUGINS_VALIDATION_001</c>，消息键按宿主承载的隔离方式取：进程外为 <see cref="PluginMessages.NotOutOfProcess"/>，Wasm 为 <see cref="PluginMessages.NotWasm"/>
    /// </remarks>
    /// <param name="manifest">清单</param>
    /// <param name="isolation">宿主承载的隔离方式</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="isolation"/> 不是进程外或 Wasm</exception>
    public static Result CheckKind(Manifest manifest, PluginIsolation isolation)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Kind == ManifestKind.Plugin && manifest.Isolation == isolation)
        {
            return Result.Success();
        }

        var details = MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString()));
        return isolation switch
        {
            PluginIsolation.OutOfProcess => new Error(PluginErrors.InvalidSource, PluginMessages.NotOutOfProcess, details),
            PluginIsolation.Wasm => new Error(PluginErrors.InvalidSource, PluginMessages.NotWasm, details),
            _ => throw new ArgumentOutOfRangeException(nameof(isolation), isolation, "Only the out-of-process and Wasm hosts check plugin manifests here."),
        };
    }
}
