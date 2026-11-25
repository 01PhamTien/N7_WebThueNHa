namespace Web2_ThueNha.Models;

/// <summary>
/// Quy ước tiền thuê: mỗi tháng lịch tròn tính theo MonthlyRent; phần ngày lẻ
/// tính theo MonthlyRent / 30 * số ngày. EndDate là ngày kết thúc không bao gồm.
/// </summary>
public static class RentalPricing
{
    public static RentalPriceBreakdown Calculate(
        decimal monthlyRent,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (monthlyRent <= 0 || endDate <= startDate)
            return new RentalPriceBreakdown(0, 0, 0m, 0m);

        var fullMonths = 0;
        var remainderStart = startDate;

        while (remainderStart <= DateOnly.MaxValue.AddMonths(-1))
        {
            var nextMonth = remainderStart.AddMonths(1);
            if (nextMonth > endDate)
                break;

            fullMonths++;
            remainderStart = nextMonth;
        }

        var extraDays = endDate.DayNumber - remainderStart.DayNumber;
        var dailyRate = decimal.Round(monthlyRent / 30m, 0, MidpointRounding.AwayFromZero);
        var totalAmount = decimal.Round(
            monthlyRent * fullMonths + monthlyRent * extraDays / 30m,
            0,
            MidpointRounding.AwayFromZero);

        return new RentalPriceBreakdown(fullMonths, extraDays, dailyRate, totalAmount);
    }
}

public sealed record RentalPriceBreakdown(
    int FullMonths,
    int ExtraDays,
    decimal DailyRate,
    decimal TotalAmount);
