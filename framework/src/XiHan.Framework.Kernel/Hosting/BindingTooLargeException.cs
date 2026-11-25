// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 一个绑定目录超过大小或文件数的上限
/// </summary>
/// <remarks>
/// 携带 <see cref="KernelErrors.BindingTooLarge"/>，对外就是一个 <see cref="FrameworkException"/>：消息只带绑定名、文件名与上限，不带文件内容与路径。插件宿主（<c>Plugins.Protocol</c>）认出它后取绑定名与超的是哪一个上限，写成配置失败的固定 <c>reason</c>，不带文件名（文件名就是属性名，这时还没有核对它的文法，ADR-0292、ADR-0301）。根目录下的绑定数超限不是这个类型：那时没有具体的绑定，插件宿主只读点了名的绑定，也碰不到它
/// </remarks>
/// <param name="binding">绑定名，即绑定目录的名字</param>
/// <param name="file">超限时正在读的文件名；<see cref="BindingLimit.FileCount"/> 时不用</param>
/// <param name="limit">超的是哪一个上限</param>
internal sealed class BindingTooLargeException(string binding, string file, BindingLimit limit)
    : FrameworkException(KernelErrors.BindingTooLarge, MessageOf(binding, file, limit))
{
    /// <summary>
    /// 绑定名
    /// </summary>
    public string Binding { get; } = binding;

    /// <summary>
    /// 超的是哪一个上限
    /// </summary>
    public BindingLimit Limit { get; } = limit;

    /// <summary>
    /// 异常消息
    /// </summary>
    private static string MessageOf(string binding, string file, BindingLimit limit) => limit switch
    {
        BindingLimit.FileBytes => string.Create(CultureInfo.InvariantCulture, $"File '{file}' of binding '{binding}' is larger than {BindingResolver.MaxFileBytes} bytes."),
        BindingLimit.TotalBytes => string.Create(CultureInfo.InvariantCulture, $"The files of binding '{binding}' are larger than {BindingResolver.MaxBindingBytes} bytes in total; the limit was reached while reading '{file}'."),
        BindingLimit.FileCount => string.Create(CultureInfo.InvariantCulture, $"Binding '{binding}' has more than {BindingResolver.MaxBindingFiles} files."),
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, "The binding limit is not known."),
    };
}
