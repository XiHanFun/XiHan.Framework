// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

internal sealed class Widget : IEntity<Guid>
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public int Rank { get; set; }
}
