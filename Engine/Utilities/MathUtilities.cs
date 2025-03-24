namespace Engine.Utilities;

public static class MathUtilities
{
    public static decimal Pow(decimal baseValue, int exponent)
    {
        if (exponent == 0)
        {
            return 1m;
        }

        decimal result = 1m;
        bool isNegativeExponent = exponent < 0;

        if (isNegativeExponent)
        {
            exponent = -exponent;
        }

        for (int i = 0; i < exponent; i++)
        {
            result *= baseValue;
        }

        return isNegativeExponent ? 1m / result : result;
    }

    public static decimal RoundUp(decimal value, int precision)
    {
        decimal factor = (decimal)Math.Pow(10, precision);
        return Math.Ceiling(value * factor) / factor;
    }

    public static decimal RoundDown(decimal value, int precision)
    {
        decimal factor = (decimal)Math.Pow(10, precision);
        return Math.Floor(value * factor) / factor;
    }

    public static int ExtractPrecision(decimal value)
    {
        string valueStr = value.ToString().TrimEnd('0');
        int decimalIndex = valueStr.IndexOf('.');
        return decimalIndex == -1 ? 0 : valueStr.Length - decimalIndex - 1;
    }
}