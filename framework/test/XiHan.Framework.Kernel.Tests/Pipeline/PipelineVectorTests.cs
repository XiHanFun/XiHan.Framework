// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Pipeline;

/// <summary>
/// 回放管道排序的一致性向量
/// </summary>
/// <remarks>
/// <c>pipeline/ordering.json</c>：排序、打印与报错时机（RFC-0004「排序规则」「打印格式」，ADR-0235）
/// </remarks>
public sealed class PipelineVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly OrderingVectors _vectors = SpecVectors.Load<OrderingVectors>("pipeline/ordering.json");

    [Fact]
    public void Dump_MatchesTheVectorOrder()
    {
        Assert.NotEmpty(_vectors.Cases);
        foreach (var vector in _vectors.Cases)
        {
            var builder = new PipelineBuilder();
            foreach (var step in vector.Steps)
            {
                Apply(builder, step);
            }

            Assert.Equal(vector.Dump, builder.Dump().Replace(typeof(VectorMiddleware).FullName!, "{type}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Errors_AreReportedWhenTheVectorSays()
    {
        Assert.NotEmpty(_vectors.Errors);
        foreach (var vector in _vectors.Errors)
        {
            var builder = new PipelineBuilder();
            var failedAt = -1;
            FrameworkException? failure = null;
            for (var index = 0; index < vector.Steps.Length && failure is null; index++)
            {
                try
                {
                    Apply(builder, vector.Steps[index]);
                }
                catch (FrameworkException exception)
                {
                    (failure, failedAt) = (exception, index);
                }
            }

            failure ??= Assert.Throws<FrameworkException>(() => builder.Dump());

            // 消息原文逐字相等（ADR-0236），按名字登记的实例的类型名写成 {type}
            Assert.Equal(vector.Error, failure.Code);
            Assert.Equal(vector.Message, failure.Message.Replace(typeof(VectorMiddleware).FullName!, "{type}", StringComparison.Ordinal));
            Assert.Equal(vector.At.ValueKind == JsonValueKind.Number ? vector.At.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture) : vector.At.GetString(), failedAt < 0 ? "sort" : failedAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// 执行一步：按名字登记一个实例，或声明一条按名字的约束
    /// </summary>
    private static void Apply(PipelineBuilder builder, JsonElement step)
    {
        if (step.TryGetProperty("use", out var use))
        {
            var stageName = step.GetProperty("stage").GetString();
            if (!PipelineStageExtensions.TryParse(stageName, out var stage))
            {
                // 与清单读取一致：未知阶段名报 KERNEL_PIPELINE_002
                throw new FrameworkException(KernelErrors.UnknownStage, $"Unknown pipeline stage '{stageName}'.");
            }

            var placement = step.GetProperty("placement").GetString() == "beginning" ? Placement.Beginning : Placement.End;
            builder.UseNamedInstance(use.GetString()!, new VectorMiddleware(), stage, placement, step.GetProperty("owner").GetString()!);
        }
        else if (step.TryGetProperty("before", out var before))
        {
            builder.Before(before[0].GetString()!, before[1].GetString()!);
        }
        else
        {
            var after = step.GetProperty("after");
            builder.After(after[0].GetString()!, after[1].GetString()!);
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record OrderingVectors(OrderingCase[] Cases, OrderingError[] Errors);

    /// <summary>
    /// 一组登记与期望的打印文本
    /// </summary>
    private sealed record OrderingCase(string Name, JsonElement[] Steps, string Dump);

    /// <summary>
    /// 一组登记与期望的错误码、报出的那一步（或排序时）
    /// </summary>
    private sealed record OrderingError(string Name, JsonElement[] Steps, string Error, JsonElement At, string Message);

    /// <summary>
    /// 按名字登记的向量中间件
    /// </summary>
    private sealed class VectorMiddleware : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep) => nextStep(context);
    }
}
