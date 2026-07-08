namespace SchoolAccount.ResiliencePlayground.Extensions;

public static class DateOnlyExtensions
{
    public static DateOnly ToDateOnly(this DateTime date)
    {
        return new(date.Year, date.Month, date.Day);
    }
}