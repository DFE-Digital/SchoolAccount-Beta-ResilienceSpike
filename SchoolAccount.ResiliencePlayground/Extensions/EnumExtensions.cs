using System;
using System.Text.RegularExpressions;

namespace SchoolAccount.ResiliencePlayground.Extensions;

public static class EnumExtensions
{
    public static string ToHumanString(this Enum enumValue)
    {
        var name = enumValue.ToString();
        return Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
    }
    
    public static object ToObject(this Enum enumValue)
    {
        return new
        {
            Id = enumValue.GetHashCode(),
            Label = enumValue.ToHumanString()
        };
    }
}