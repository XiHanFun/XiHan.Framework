// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 绑定目录里的文件超过大小上限
/// </summary>
/// <remarks>
/// 携带 <see cref="KernelErrors.BindingTooLarge"/>，对外就是一个 <see cref="FrameworkException"/>：消息只带绑定名、文件名与上限，不带文件内容。插件宿主（<c>Plugins.Protocol</c>）认出它后取绑定名与超的是哪一个上限，写成配置失败的固定 <c>reason</c>，不带文件名（文件名就是属性名，这时还没有核对它的文法，ADR-0292）
/// </remarks>
/// <param name="binding">绑定名，即绑定目录的名字</param>
/// <param name="file">超限时正在读的文件名</param>
/// <param name="total">超的是一个绑定合计的上限（<see cref="BindingResolver.MaxBindingBytes"/>）；为假时是单个文件的上限（<see cref="BindingResolver.MaxFileBytes"/>）</param>
internal sealed class BindingTooLargeException(string binding, string file, bool total)
    : FrameworkException(KernelErrors.BindingTooLarge, total
        ? string.Create(CultureInfo.InvariantCulture, $"The files of binding '{binding}' are larger than {BindingResolver.MaxBindingBytes} bytes in total; the limit was reached while reading '{file}'.")
        : string.Create(CultureInfo.InvariantCulture, $"File '{file}' of binding '{binding}' is larger than {BindingResolver.MaxFileBytes} bytes."))
{
    /// <summary>
    /// 绑定名
    /// </summary>
    public string Binding { get; } = binding;

    /// <summary>
    /// 超的是不是一个绑定合计的上限
    /// </summary>
    public bool Total { get; } = total;
}
