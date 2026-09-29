// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Tests.Core;

/// <summary>
/// 详细年龄计算测试
/// </summary>
/// <remarks>
/// 原实现在日不够减时借"参考日期前一个月"的天数，跨大小月时借不够：
/// 出生 2024-01-31、参考 2024-03-01 会算出 (0, 1, -1)，天数为负。
/// </remarks>
public class DateTimeHelperDetailedAgeTests
{
    /// <summary>
    /// 跨大小月借位后天数不为负
    /// </summary>
    [Theory]
    [InlineData("2024-01-31", "2024-03-01", 0, 1, 1)]
    [InlineData("2024-01-31", "2024-02-29", 0, 1, 0)]
    [InlineData("2024-01-30", "2024-03-01", 0, 1, 1)]
    [InlineData("2023-01-31", "2023-03-01", 0, 1, 1)]
    [InlineData("2023-08-31", "2023-10-01", 0, 1, 1)]
    public void GetDetailedAge_AcrossMonthsOfDifferentLength_KeepsDaysNonNegative(
        string birth, string reference, int years, int months, int days)
    {
        Assert.Equal((years, months, days), DateTimeHelper.GetDetailedAge(DateTime.Parse(birth), DateTime.Parse(reference)));
    }

    /// <summary>
    /// 常规日期的年月日分量正确
    /// </summary>
    [Theory]
    [InlineData("2000-05-15", "2024-05-15", 24, 0, 0)]
    [InlineData("2000-05-15", "2024-05-14", 23, 11, 29)]
    [InlineData("2000-05-15", "2024-06-20", 24, 1, 5)]
    [InlineData("2000-05-15", "2000-05-15", 0, 0, 0)]
    [InlineData("2000-02-29", "2023-02-28", 23, 0, 0)]
    [InlineData("2000-02-29", "2024-02-29", 24, 0, 0)]
    public void GetDetailedAge_ReturnsExpectedComponents(string birth, string reference, int years, int months, int days)
    {
        Assert.Equal((years, months, days), DateTimeHelper.GetDetailedAge(DateTime.Parse(birth), DateTime.Parse(reference)));
    }

    /// <summary>
    /// 出生日期晚于参考日期时返回全零
    /// </summary>
    [Fact]
    public void GetDetailedAge_WhenBirthDateInFuture_ReturnsZero()
    {
        Assert.Equal((0, 0, 0), DateTimeHelper.GetDetailedAge(new DateTime(2030, 1, 1), new DateTime(2024, 1, 1)));
    }

    /// <summary>
    /// 任意日期组合下三个分量都不为负，且月份不超过 11 天不超过 31
    /// </summary>
    /// <remarks>
    /// 遍历两年内的所有出生日与参考日组合，直接把"分量合法"这条不变量锁死，
    /// 不依赖挑选特定的边界样例。
    /// </remarks>
    [Fact]
    public void GetDetailedAge_ComponentsAreAlwaysWithinValidRange()
    {
        var failures = new List<string>();

        for (var birth = new DateTime(2023, 1, 1); birth <= new DateTime(2024, 12, 31); birth = birth.AddDays(1))
        {
            for (var reference = birth; reference <= birth.AddDays(400); reference = reference.AddDays(17))
            {
                var (years, months, days) = DateTimeHelper.GetDetailedAge(birth, reference);

                if (years < 0 || months is < 0 or > 11 || days is < 0 or > 31)
                {
                    failures.Add($"{birth:yyyy-MM-dd} -> {reference:yyyy-MM-dd} = ({years}, {months}, {days})");
                    if (failures.Count >= 5)
                    {
                        break;
                    }
                }
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// 把算出的年月日加回出生日期能回到参考日期
    /// </summary>
    /// <remarks>这条把"分量之和确实等于经过的时间"锁住，光断言非负挡不住少算一个月这类错误。</remarks>
    [Fact]
    public void GetDetailedAge_ComponentsAddBackToReferenceDate()
    {
        var failures = new List<string>();

        for (var birth = new DateTime(2023, 1, 1); birth <= new DateTime(2024, 12, 31); birth = birth.AddDays(11))
        {
            for (var reference = birth; reference <= birth.AddDays(800); reference = reference.AddDays(23))
            {
                var (years, months, days) = DateTimeHelper.GetDetailedAge(birth, reference);
                var rebuilt = birth.AddYears(years).AddMonths(months).AddDays(days);

                if (rebuilt.Date != reference.Date)
                {
                    failures.Add($"{birth:yyyy-MM-dd} -> {reference:yyyy-MM-dd} = ({years}, {months}, {days})，加回得到 {rebuilt:yyyy-MM-dd}");
                    if (failures.Count >= 5)
                    {
                        break;
                    }
                }
            }
        }

        Assert.Empty(failures);
    }
}
