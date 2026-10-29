// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// <see cref="PipelineBuilder"/> 的行为。
/// </summary>
/// <remarks>
/// 阶段内排序键、相对约束、环与跨阶段约束的报错、重复注册、委托构建顺序与打印格式。
/// </remarks>
public sealed class PipelineBuilderTests
{
    [Fact]
    public async Task UseNamedInstance_AllowsSeveralInstancesOfOneType_AndRunsEachOfThem()
    {
        var log = new List<string>();
        var services = new ServiceCollection().BuildServiceProvider();
        var builder = new PipelineBuilder()
            .UseNamedInstance("first", new MiddlewareA(log), PipelineStage.Observe, owner: "xihan.plugins.a")
            .UseNamedInstance("second", new MiddlewareA(log), PipelineStage.Observe, owner: "xihan.plugins.b");

        Assert.Equal(["first", "second"], builder.Describe().Select(static d => d.Name));
        Assert.Contains("MiddlewareA[first]", builder.Dump(), StringComparison.Ordinal);
        Assert.Contains("MiddlewareA[second]", builder.Dump(), StringComparison.Ordinal);

        var pipeline = builder.Build(_ => ValueTask.CompletedTask);
        await pipeline(App.CreateContext(services, CancellationToken.None));
        Assert.Equal(["enter:A", "enter:A", "exit:A", "exit:A"], log);
    }

    [Fact]
    public void UseNamedInstance_RejectsDuplicateNames_AndTypeConstraintsOnNamedInstances()
    {
        var log = new List<string>();
        var builder = new PipelineBuilder()
            .UseNamedInstance("first", new MiddlewareA(log), PipelineStage.Observe)
            .UseNamedInstance("second", new MiddlewareA(log), PipelineStage.Observe)
            .UseAt<MiddlewareB>(PipelineStage.Observe);

        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.UseNamedInstance("first", new MiddlewareA(log), PipelineStage.Observe)).Code);
        // 同类型有多个命名实例时，按类型的约束无法指向其中某一个
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.Before<MiddlewareA, MiddlewareB>()).Code);
        // 约束的目标在排序时才解析：指向有多个命名实例的类型同样是登记错误
        builder.After<MiddlewareB, MiddlewareA>();
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => builder.Describe()).Code);
    }

    [Fact]
    public void Describe_OrdersByStageThenPlacementThenOwnerThenRegistration()
    {
        var builder = new PipelineBuilder()
            .UseAt<MiddlewareA>(PipelineStage.Handle)
            .UseAt<MiddlewareB>(PipelineStage.Ingress, owner: "xihan.z")
            .UseAt<MiddlewareC>(PipelineStage.Ingress, owner: "xihan.a")
            .UseAt<MiddlewareD>(PipelineStage.Ingress, Placement.Beginning, owner: "xihan.z");

        var order = builder.Describe().Select(static d => d.Middleware).ToList();

        Assert.Equal([typeof(MiddlewareD), typeof(MiddlewareC), typeof(MiddlewareB), typeof(MiddlewareA)], order);
    }

    [Fact]
    public void BeforeAndAfter_ReorderWithinStage()
    {
        var builder = new PipelineBuilder()
            .UseAt<MiddlewareA>(PipelineStage.Handle)
            .UseAt<MiddlewareB>(PipelineStage.Handle)
            .UseAt<MiddlewareC>(PipelineStage.Handle)
            .Before<MiddlewareC, MiddlewareA>()
            .After<MiddlewareB, MiddlewareA>();

        var order = builder.Describe().Select(static d => d.Middleware).ToList();

        Assert.Equal([typeof(MiddlewareC), typeof(MiddlewareA), typeof(MiddlewareB)], order);
    }

    [Fact]
    public void CyclicConstraints_ThrowPipelineCycle()
    {
        var builder = new PipelineBuilder()
            .UseAt<MiddlewareA>(PipelineStage.Handle)
            .UseAt<MiddlewareB>(PipelineStage.Handle)
            .Before<MiddlewareA, MiddlewareB>()
            .Before<MiddlewareB, MiddlewareA>();

        var exception = Assert.Throws<FrameworkException>(() => builder.Describe());

        Assert.Equal(KernelErrors.PipelineCycle, exception.Code);
    }

    [Fact]
    public void CrossStageConstraint_ThrowsRegistrationError()
    {
        var builder = new PipelineBuilder()
            .UseAt<MiddlewareA>(PipelineStage.Handle)
            .UseAt<MiddlewareB>(PipelineStage.Ingress)
            .Before<MiddlewareA, MiddlewareB>();

        var exception = Assert.Throws<FrameworkException>(() => builder.Describe());

        Assert.Equal(KernelErrors.PipelineRegistration, exception.Code);
    }

    [Fact]
    public void DuplicateMiddleware_ThrowsRegistrationError()
    {
        var builder = new PipelineBuilder().UseAt<MiddlewareA>(PipelineStage.Handle);

        var exception = Assert.Throws<FrameworkException>(() => builder.UseAt<MiddlewareA>(PipelineStage.Ingress));

        Assert.Equal(KernelErrors.PipelineRegistration, exception.Code);
        Assert.Equal(KernelErrors.PipelineRegistration, Assert.Throws<FrameworkException>(() => new PipelineBuilder().Before<MiddlewareA, MiddlewareB>()).Code);
    }

    [Fact]
    public async Task Build_InvokesMiddlewareInOrderAroundTerminal()
    {
        var log = new List<string>();
        var services = new ServiceCollection()
            .AddSingleton(log)
            .AddSingleton<MiddlewareA>()
            .AddSingleton<MiddlewareB>()
            .BuildServiceProvider();
        // B 先注册但在更靠后的阶段，执行顺序必须由阶段决定。
        var pipeline = new PipelineBuilder()
            .UseAt<MiddlewareB>(PipelineStage.Handle)
            .UseAt<MiddlewareA>(PipelineStage.Ingress)
            .Build(context =>
            {
                log.Add("terminal");
                context.SetOutcome(Result.Success());
                return ValueTask.CompletedTask;
            });

        var context = App.CreateContext(services, CancellationToken.None);
        await pipeline(context);

        Assert.Equal(["enter:A", "enter:B", "terminal", "exit:B", "exit:A"], log);
        Assert.True(context.GetOutcome().ValueOrThrow().IsSuccess);
    }

    [Fact]
    public void Dump_PrintsStageAndTypePerLine()
    {
        var dump = new PipelineBuilder()
            .UseAt<MiddlewareA>(PipelineStage.Egress)
            .UseAt<MiddlewareB>(PipelineStage.Ingress)
            .Dump();

        Assert.Equal(
            $"ingress      {typeof(MiddlewareB).FullName}\negress       {typeof(MiddlewareA).FullName}\n",
            dump);
    }

    [Fact]
    public void Use_DefaultsToTransactionStage()
    {
        var descriptor = Assert.Single(new PipelineBuilder().Use<MiddlewareD>().Describe());

        Assert.Equal(PipelineStage.Transaction, descriptor.Stage);
        Assert.Equal(PipelineBuilder.AppOwner, descriptor.Owner);
    }
}
