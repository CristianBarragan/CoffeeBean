namespace CoffeeBeanery.GraphQL.Helper
{
    public static class StringExtensions
    {
        public static bool Matches(this string? input, string comparison)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            return string.Compare(input, comparison, StringComparison.OrdinalIgnoreCase) == 0 &&
                   input.Length == comparison.Length;
        }
        
        public static string CleanJsonString(this string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return input.Replace(" ", "").Replace("{", "").Replace("}", "").Replace("\"", "");
        }

        public static string ToSnakeCase(this string input, int numberOfSnakeChar = 1)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var underscored = string.Empty;
            for (var i = 0; i < numberOfSnakeChar; i++)
            {
                underscored += '_';
            }

            return input + underscored;
        }

        public static string ToUpperCamelCase(this string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return field;
            }

            return field.Substring(0, 1).ToUpper() + field.Substring(1, field.Length - 1);
        }

        public static string ToLowerCamelCase(this string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return field;
            }

            return field.Substring(0, 1).ToLower() + field.Substring(1, field.Length - 1);
        }
        
        public static string ToCamelCase(this string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return field;
            }

            return field.Substring(0, 1).ToLower() + field.Substring(1, field.Length - 1);
        }

        public static string Sanitize(this string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return field;
            }

            return field.Replace("'", "").Replace("\"", "").Trim();
        }
    }
}