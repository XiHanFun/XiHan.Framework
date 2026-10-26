// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 一个最小的待迁移项目：项目文件、两份 C# 源码（一份 LF、一份 CRLF 带 BOM）、一份待转换的 JSON，
/// 以及 bin 下与生成的、不该被碰的文件；用完删掉。
/// </summary>
internal sealed class MigrationFixture : IDisposable
{
    /// <summary>
    /// 不该被碰的文件内容；迁移后应原样。
    /// </summary>
    public const string UntouchedSource = "public sealed class LegacyClock { }\n";

    /// <summary>
    /// <c>OrderService.cs</c> 的原文（CRLF）。
    /// </summary>
    public const string OrderServiceSource = "using System;\r\n\r\nnamespace Shop.Orders.Services;\r\n\r\npublic sealed class OrderService(LegacyClock clock)\r\n{\r\n    public LegacyTimer Timer { get; } = new();\r\n\r\n    public DateTime Placed => clock.Now;\r\n}\r\n";

    /// <summary>
    /// 根目录。
    /// </summary>
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "xihan-migrate-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 项目目录 <c>Shop.Orders</c>。
    /// </summary>
    public string Project => Path.Combine(Root, "Shop.Orders");

    /// <summary>
    /// 写出夹具文件。
    /// </summary>
    public MigrationFixture()
    {
        Directory.CreateDirectory(Path.Combine(Project, "Services"));
        Directory.CreateDirectory(Path.Combine(Project, "bin", "Debug"));
        File.WriteAllText(Path.Combine(Project, "Shop.Orders.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.WriteAllText(Path.Combine(Project, "Services", "LegacyClock.cs"), "// 订单模块的时钟\nusing System;\n\nnamespace Shop.Orders.Services;\n\npublic sealed class LegacyClock\n{\n    public DateTime Now => DateTime.UtcNow;\n}\n");
        File.WriteAllBytes(Path.Combine(Project, "Services", "OrderService.cs"), [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes(OrderServiceSource)]);
        File.WriteAllText(Path.Combine(Project, "settings.legacy.json"), "{ \"retries\": 3 }\n");
        File.WriteAllText(Path.Combine(Project, "bin", "Debug", "Skipped.cs"), UntouchedSource);
        File.WriteAllText(Path.Combine(Project, "Services", "Proxy.g.cs"), UntouchedSource);
    }

    /// <summary>
    /// 项目内文件的绝对路径。
    /// </summary>
    public string PathOf(string relative) => Path.Combine(Project, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// 项目下全部文件的快照：相对路径 → 字节。
    /// </summary>
    public SortedDictionary<string, byte[]> Snapshot()
        => new(Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(file => Path.GetRelativePath(Root, file).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
