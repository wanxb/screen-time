using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class AppCategoryClassifier
{
    private readonly AppCategoryRuleSet _ruleSet;

    public AppCategoryClassifier(AppCategoryRuleSet ruleSet)
    {
        _ruleSet = ruleSet;
    }

    public AppCategory Classify(ForegroundAppInfo app)
    {
        foreach (var rule in _ruleSet.Rules)
        {
            if (Contains(rule.ProcessNames, app.ProcessName)
                || Contains(rule.PathKeywords, app.ExecutablePath)
                || Contains(rule.NameKeywords, app.Name))
            {
                return rule.Category;
            }
        }

        return AppCategory.Other;
    }

    private static bool Contains(IEnumerable<string> needles, string haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        return needles.Any(needle =>
            !string.IsNullOrWhiteSpace(needle)
            && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
