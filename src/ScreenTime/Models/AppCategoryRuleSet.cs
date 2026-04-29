namespace ScreenTime.Models;

public sealed class AppCategoryRuleSet
{
    public int Version { get; set; } = 1;
    public List<AppCategoryRule> Rules { get; set; } = [];
}

public sealed class AppCategoryRule
{
    public AppCategory Category { get; set; } = AppCategory.Other;
    public List<string> ProcessNames { get; set; } = [];
    public List<string> PathKeywords { get; set; } = [];
    public List<string> NameKeywords { get; set; } = [];
    public List<string> WindowTitleKeywords { get; set; } = [];
}
