// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing.Workspace;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>status-order.json</c>：定义与实例状态的次序
/// </summary>
/// <remarks>
/// 规范 RFC-0021「定义」的「列表排序」（ADR-0103、ADR-0162、ADR-0277）：按状态排序时按生命周期的先后比较，不按状态名的字母序，各存储共用这一份次序。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class WorkflowStatusOrderVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly StatusOrderVectors _vectors = SpecVectors.Read<StatusOrderVectors>("workflow", "status-order.json");

    [Fact]
    public void DefinitionStatuses_SortInTheOrderOfTheVectors()
    {
        // 全部枚举值按序号升序排出来就是向量的次序：新增的状态没在规范里定位置时在这里失败
        var ordered = Enum.GetValues<WorkflowDefinitionStatus>().OrderBy(static status => WorkflowStatusOrder.Of(status)).Select(static status => status.ToString());

        Assert.Equal(_vectors.Definition, ordered);
        Assert.Distinct(Enum.GetValues<WorkflowDefinitionStatus>().Select(static status => WorkflowStatusOrder.Of(status)));
    }

    [Fact]
    public void InstanceStatuses_SortInTheOrderOfTheVectors()
    {
        var ordered = Enum.GetValues<WorkflowInstanceStatus>().OrderBy(static status => WorkflowStatusOrder.Of(status)).Select(static status => status.ToString());

        Assert.Equal(_vectors.Instance, ordered);
        Assert.Distinct(Enum.GetValues<WorkflowInstanceStatus>().Select(static status => WorkflowStatusOrder.Of(status)));
    }

    [Fact]
    public void TheVectors_DoNotListTheStatusesAlphabetically()
    {
        // 这份次序存在的理由就是它不同于字母序：向量换成字母序时上面两条的回放也就不再核对什么
        Assert.NotEmpty(_vectors.Definition);
        Assert.NotEmpty(_vectors.Instance);
        Assert.NotEqual(_vectors.Definition.Order(StringComparer.Ordinal), _vectors.Definition);
        Assert.NotEqual(_vectors.Instance.Order(StringComparer.Ordinal), _vectors.Instance);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Definition">定义的状态，按升序</param>
    /// <param name="Instance">实例的状态，按升序</param>
    private sealed record StatusOrderVectors(string[] Definition, string[] Instance);
}
