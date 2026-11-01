// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite 上的 <see cref="DateTimeOffset"/> 存成 UTC ticks 整数（ADR-0091）。
/// </summary>
/// <remarks>
/// SQLite 的 EF Core Provider 把 <see cref="DateTimeOffset"/> 存成文本，翻译不了大小比较与排序。<c>ApplyPersistenceConventions(Database)</c> 在 SQLite 上把模型里全部这类属性（含可空、复杂类型里的）换成 UTC ticks：比较与排序按时刻、不受写入时的偏移影响，往返保留 100 纳秒精度、读回偏移为零；应用自己配了转换或存储类型的属性不动；别的 Provider 不动。
/// </remarks>
public sealed class EntityFrameworkCoreDateTimeOffsetTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task OnSqlite_TimesAreStoredAsUtcTicks_AndCompareAndSortAsInstants()
    {
        // 同一天里五个时刻，写入时带不同的偏移；按时刻排是 first、tick、half、late，after 在截止之后
        var first = Row(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var tick = Row(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1));
        var half = Row(new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero));
        var late = Row(new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-5)));
        var after = Row(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
        first.Due = late.At;
        await using (var context = Timed())
        {
            await context.Database.EnsureCreatedAsync();
            context.AddRange(late, after, half, first, tick);
            await context.SaveChangesAsync();
        }

        await using (var context = Timed())
        {
            var at = context.Model.FindEntityType(typeof(TimedRow))!.FindProperty(nameof(TimedRow.At))!;
            Assert.IsType<UtcTicksConverter>(at.GetTypeMapping().Converter);
            Assert.Equal("INTEGER", at.GetColumnType());

            // 截止时刻用另一个偏移写：01:00Z，含 late（-05:00 写入的 01:00Z）
            var cutoff = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(8));
            var ascending = await context.Set<TimedRow>().Where(row => row.At <= cutoff).OrderBy(static row => row.At).Select(static row => row.Id).ToListAsync();
            Assert.Equal([first.Id, tick.Id, half.Id, late.Id], ascending);
            var descending = await context.Set<TimedRow>().OrderByDescending(static row => row.At).Select(static row => row.Id).ToListAsync();
            Assert.Equal([after.Id, late.Id, half.Id, tick.Id, first.Id], descending);

            // 可空的时刻：判空与比较照常翻译
            var dueAfter = await context.Set<TimedRow>().Where(row => row.Due != null && row.Due > first.At).Select(static row => row.Id).ToListAsync();
            Assert.Equal([first.Id], dueAfter);
            Assert.Equal(4, await context.Set<TimedRow>().CountAsync(static row => row.Due == null));

            // 往返保留 100 纳秒精度，读回是偏移为零的同一时刻
            var read = await context.Set<TimedRow>().SingleAsync(row => row.Id == tick.Id);
            Assert.Equal((tick.At.UtcTicks, TimeSpan.Zero), (read.At.UtcTicks, read.At.Offset));
            var readFirst = await context.Set<TimedRow>().SingleAsync(row => row.Id == first.Id);
            Assert.Equal((first.At, late.At.UtcTicks, TimeSpan.Zero), (readFirst.At, readFirst.Due!.Value.UtcTicks, readFirst.At.Offset));

            // 库里存的就是 UTC ticks 整数
            Assert.Equal(["integer"], await context.Database.SqlQueryRaw<string>("SELECT DISTINCT typeof(\"At\") AS \"Value\" FROM \"TimedRow\"").ToListAsync());
            Assert.Equal(after.At.UtcTicks, await context.Database.SqlQueryRaw<long>("SELECT max(\"At\") AS \"Value\" FROM \"TimedRow\"").SingleAsync());
            Assert.Equal(late.At.UtcTicks, await context.Database.SqlQueryRaw<long>("SELECT \"Due\" AS \"Value\" FROM \"TimedRow\" WHERE \"Due\" IS NOT NULL").SingleAsync());
        }
    }

    [Fact]
    public void ExplicitConversions_AreKept()
    {
        using var context = new ConvertedTimedDbContext(new DbContextOptionsBuilder<ConvertedTimedDbContext>().UseSqlite(_database.ConnectionString).Options);
        var entity = context.Model.FindEntityType(typeof(TimedRow))!;

        // ConfigureConventions 里统一配的与 OnModelCreating 里单独配的都保留：已有库的存储格式不变
        Assert.IsType<DateTimeOffsetToBinaryConverter>(entity.FindProperty(nameof(TimedRow.At))!.GetTypeMapping().Converter);
        Assert.Equal(typeof(string), entity.FindProperty(nameof(TimedRow.Due))!.GetTypeMapping().Converter?.ProviderClrType);
    }

    [Fact]
    public void Conversion_CoversComplexTypes_OnlyOnSqlite()
    {
        foreach (var (provider, converted) in new[] { (ModelBuilderExtensions.SqliteProvider, true), (ModelBuilderExtensions.NpgsqlProvider, false), (ModelBuilderExtensions.SqlServerProvider, false) })
        {
            var modelBuilder = new ModelBuilder(SqliteConventionSetBuilder.Build());
            modelBuilder.Entity<TimedRow>();
            modelBuilder.Entity<WindowedRow>().ComplexProperty(static row => row.Window);
            ModelBuilderExtensions.Apply(modelBuilder, provider, relational: true);

            var timed = modelBuilder.Model.FindEntityType(typeof(TimedRow))!;
            var window = modelBuilder.Model.FindEntityType(typeof(WindowedRow))!.FindComplexProperty(nameof(WindowedRow.Window))!.ComplexType;
            var converters = new[] { timed.FindProperty(nameof(TimedRow.At))!, timed.FindProperty(nameof(TimedRow.Due))!, window.FindProperty(nameof(TimeWindow.Start))!, window.FindProperty(nameof(TimeWindow.End))! }
                .Select(static property => property.GetValueConverter());
            Assert.All(converters, converter => Assert.Equal(converted, converter is UtcTicksConverter));
        }
    }

    [Fact]
    public void Converter_RoundTripsTheInstant_AtFullPrecision()
    {
        var converter = UtcTicksConverter.Instance;
        var moment = new DateTimeOffset(2026, 11, 1, 23, 59, 59, TimeSpan.FromHours(8)).AddTicks(1234567);
        var stored = (long)converter.ConvertToProvider(moment)!;
        Assert.Equal(moment.UtcTicks, stored);
        var restored = (DateTimeOffset)converter.ConvertFromProvider(stored)!;
        Assert.Equal((moment, moment.UtcTicks, TimeSpan.Zero), (restored, restored.UtcTicks, restored.Offset));
        Assert.Equal(DateTimeOffset.MinValue, converter.ConvertFromProvider(converter.ConvertToProvider(DateTimeOffset.MinValue)));
        Assert.Equal(DateTimeOffset.MaxValue, converter.ConvertFromProvider(converter.ConvertToProvider(DateTimeOffset.MaxValue)));
    }

    /// <summary>
    /// 一行：给定发生时刻，没有到期时刻。
    /// </summary>
    private static TimedRow Row(DateTimeOffset at) => new() { Id = Guid.CreateVersion7(), At = at };

    /// <summary>
    /// 连到本测试数据库、只登记 <see cref="TimedRow"/> 的上下文。
    /// </summary>
    private TimedDbContext Timed()
        => new(new DbContextOptionsBuilder<TimedDbContext>().UseSqlite(_database.ConnectionString).Options);
}
