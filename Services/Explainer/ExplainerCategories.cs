namespace PostFlow.Services.Explainer
{
    public static class ExplainerCategories
    {
        public const string Tech = "tech";
        public const string Finance = "finance";
        public const string General = "general";

        public static bool IsFinance(string? category) => Is(category, Finance);

        public static bool IsGeneral(string? category) => Is(category, General);

        private static bool Is(string? category, string expected) =>
            string.Equals(category, expected, StringComparison.OrdinalIgnoreCase);
    }
}
