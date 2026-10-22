// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 文法本身的正反用例：保证门禁不是"什么都放行"。
/// </summary>
public sealed class PackageGrammarTests
{
    [Theory]
    [InlineData("XiHan.Framework.Primitives", 0)]
    [InlineData("XiHan.Framework.Kernel", 1)]
    [InlineData("XiHan.Framework.Kernel.Analyzers", 7)]
    [InlineData("XiHan.Framework.Domain", 2)]
    [InlineData("XiHan.Framework.Persistence.Abstractions", 2)]
    [InlineData("XiHan.Framework.Persistence.InMemory", 3)]
    [InlineData("XiHan.Framework.Persistence.EntityFrameworkCore", 3)]
    [InlineData("XiHan.Framework.Persistence.Conformance", 7)]
    [InlineData("XiHan.Framework.Messaging.RabbitMQ", 3)]
    [InlineData("XiHan.Framework.ObjectStorage.S3", 3)]
    [InlineData("XiHan.Framework.Hosting.AspNetCore", 4)]
    [InlineData("XiHan.Framework.Protocols.Mcp", 4)]
    [InlineData("XiHan.Framework.Plugins.OutOfProcess", 5)]
    [InlineData("XiHan.Framework.Profiles.Full", 6)]
    [InlineData("XiHan.Framework.Testing", 7)]
    [InlineData("XiHan.Framework.Cli", 7)]
    [InlineData("XiHan.Framework.Compat.V4", 7)]
    public void TryValidate_AcceptsWellFormedIds(string packageId, int expectedRing)
    {
        var ok = PackageGrammar.TryValidate(packageId, Vocabulary.SubsystemWords, out var reason);

        Assert.True(ok, reason);
        Assert.Equal(expectedRing, PackageGrammar.Ring(packageId));
    }

    [Theory]
    [InlineData("XiHan.Framework.Domain", true)]
    [InlineData("XiHan.Framework.Persistence.Abstractions", true)]
    [InlineData("XiHan.Framework.Persistence.InMemory", false)]
    [InlineData("XiHan.Framework.Kernel", false)]
    public void IsContract_RecognizesAbstractionsAndDomain(string packageId, bool expected)
        => Assert.Equal(expected, PackageGrammar.IsContract(packageId));

    [Theory]
    [InlineData("XiHan.Framework.Abi")]
    [InlineData("XiHan.Framework.Data.Abstractions")]
    [InlineData("XiHan.Framework.Caching.Default")]
    [InlineData("XiHan.Framework.Caching.Core")]
    [InlineData("XiHan.Framework.Caching")]
    [InlineData("XiHan.Framework.Conformance.Caching")]
    [InlineData("XiHan.Framework.Identity.Jwt")]
    [InlineData("XiHan.Framework.Tenancy.Abstractions")]
    [InlineData("XiHan.Framework.Storage.Local")]
    [InlineData("XiHan.Framework.Protocol.Http")]
    [InlineData("XiHan.Framework.Plugins.Assembly")]
    [InlineData("XiHan.Framework.Profile.Enterprise")]
    [InlineData("XiHan.Framework.Hosting.Worker")]
    [InlineData("XiHan.Framework.Kernel.Hosting")]
    [InlineData("XiHan.Framework.Persistence.EntityFrameworkCore.SqlServer")]
    [InlineData("XiHan.Framework.Compat.Legacy")]
    [InlineData("XiHan.Persistence.Abstractions")]
    public void TryValidate_RejectsIllFormedIds(string packageId)
    {
        var ok = PackageGrammar.TryValidate(packageId, Vocabulary.SubsystemWords, out var reason);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }
}
