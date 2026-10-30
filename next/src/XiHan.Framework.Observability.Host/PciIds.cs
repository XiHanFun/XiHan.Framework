// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 按 <c>pci.ids</c> 查厂商名与设备名。
/// </summary>
/// <remarks>
/// 用第一个存在的文件；厂商行顶格（<c>10de  NVIDIA Corporation</c>），设备行一个制表符缩进，两个制表符的子系统行与 <c>C </c> 开头的类别段跳过。没有文件或查不到为空。
/// </remarks>
/// <param name="paths">候选文件路径。</param>
internal sealed class PciIds(IEnumerable<string> paths)
{
    /// <summary>
    /// 查厂商名与设备名。
    /// </summary>
    public (string? Vendor, string? Device) Find(string? vendorId, string? deviceId)
    {
        if (vendorId is null || paths.FirstOrDefault(File.Exists) is not { } path)
        {
            return (null, null);
        }

        string? vendor = null;
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (line[0] != '\t')
                {
                    // 下一个厂商或类别段：已找到的厂商下没有这个设备
                    if (vendor is not null || line.StartsWith("C ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    vendor = Entry(line, vendorId);
                    continue;
                }

                if (vendor is not null && deviceId is not null && line.Length > 1 && line[1] != '\t' && Entry(line[1..], deviceId) is { } device)
                {
                    return (vendor, device);
                }
            }
        }
        catch (Exception exception) when (Readings.IsUnreadable(exception))
        {
            return (null, null);
        }

        return (vendor, null);
    }

    /// <summary>
    /// 行首四位 ID 相符时取名称。
    /// </summary>
    private static string? Entry(string line, string id)
        => line.Length > 6 && line.StartsWith(id, StringComparison.OrdinalIgnoreCase) && line[4] == ' ' ? Readings.Clean(line[4..]) : null;
}
