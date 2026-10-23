// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 一个最小的 4.x 项目：项目文件、模块类、带注入标记的服务，以及 bin 下一个不该被碰的文件；用完删掉。
/// </summary>
internal sealed class MigrationFixture : IDisposable
{
    /// <summary>
    /// bin 下文件的内容；迁移后应原样。
    /// </summary>
    public const string SkippedSource = "public class XiHanSkippedModule : XiHanModule { }\n";

    /// <summary>
    /// 根目录。
    /// </summary>
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "xihan-migrate-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 项目目录 <c>Shop.Orders</c>。
    /// </summary>
    public string Project => Path.Combine(Root, "Shop.Orders");

    /// <summary>
    /// bin 下的文件。
    /// </summary>
    public string Skipped => Path.Combine(Project, "bin", "Debug", "Skipped.cs");

    /// <summary>
    /// 写出夹具文件。
    /// </summary>
    public MigrationFixture()
    {
        Directory.CreateDirectory(Path.Combine(Project, "Services"));
        Directory.CreateDirectory(Path.GetDirectoryName(Skipped)!);
        File.WriteAllText(Path.Combine(Project, "Shop.Orders.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="XiHan.Framework.Core" Version="4.8.0" />
              </ItemGroup>
            </Project>

            """);
        File.WriteAllText(Path.Combine(Project, "XiHanShopOrdersModule.cs"), """
            using XiHan.Framework.Core.Modularity;

            namespace Shop.Orders;

            [DependsOn(typeof(XiHanDataModule))]
            public class XiHanShopOrdersModule : XiHanModule
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    context.Services.AddOrders();
                }
            }

            """);
        File.WriteAllText(Path.Combine(Project, "Services", "OrderService.cs"), """
            using XiHan.Framework.Core.DependencyInjection;

            namespace Shop.Orders.Services;

            public sealed class OrderService : IOrderService, ITransientDependency
            {
            }

            """);
        File.WriteAllText(Skipped, SkippedSource);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
