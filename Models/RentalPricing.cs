namespace Web2_ThueNha.Models;

/// <summary>
/// Quy ước tiền thuê: mỗi tháng lịch tròn tính theo MonthlyRent; phần ngày lẻ
/// tính theo MonthlyRent / 30 * số ngày. Thời hạn dưới một tháng vẫn thu tối thiểu một tháng.
/// EndDate là ngày kết thúc không bao gồm.
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

        // Mỗi kỳ tháng được tính từ ngày bắt đầu ban đầu, tránh trôi ngày
        // khi ngày 29/30/31 phải rút ngắn ở tháng 2 hoặc tháng có ít ngày.
        var fullMonths = Math.Max(
            0,
            (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month);

        while (fullMonths > 0 && startDate.AddMonths(fullMonths) > endDate)
            fullMonths--;

        while (true)
        {
            try
            {
                if (startDate.AddMonths(fullMonths + 1) > endDate)
                    break;

                fullMonths++;
            }
            catch (ArgumentOutOfRangeException)
            {
                break;
            }
        }

        // Đây là hệ thống thuê nhà theo giá tháng, không phải đặt phòng theo đêm:
        // mọi hợp đồng có thời hạn ngắn hơn một tháng đều áp dụng mức thuê tối thiểu một tháng.
        if (fullMonths == 0)
            return new RentalPriceBreakdown(1, 0, decimal.Round(monthlyRent / 30m, 0, MidpointRounding.AwayFromZero), monthlyRent);

        var remainderStart = startDate.AddMonths(fullMonths);
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
