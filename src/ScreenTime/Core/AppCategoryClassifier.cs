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
        var bestCategory = AppCategory.Other;
        var bestScore = 0;

        foreach (var rule in _ruleSet.Rules)
        {
            var score = Score(rule, app);
            if (score > bestScore)
            {
                bestScore = score;
                bestCategory = rule.Category;
            }
        }

        return bestCategory;
    }

    private static int Score(AppCategoryRule rule, ForegroundAppInfo app)
    {
        var score = 0;

        if (ContainsExact(rule.ProcessNames, app.ProcessName))
        {
            score += 100;
        }

        if (Contains(rule.PathKeywords, app.ExecutablePath))
        {
            score += 70;
        }

        if (Contains(rule.NameKeywords, app.Name))
        {
            score += 50;
        }

        if (Contains(rule.WindowTitleKeywords, app.WindowTitle))
        {
            score += 45;
        }

        return score;
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

    private static bool ContainsExact(IEnumerable<string> needles, string haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        return needles.Any(needle =>
            !string.IsNullOrWhiteSpace(needle)
            && string.Equals(needle, haystack, StringComparison.OrdinalIgnoreCase));
    }
}
