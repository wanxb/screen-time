using ScreenTime.Core;
using ScreenTime.Models;
using ScreenTime.Services;
using ScreenTime.Storage;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Cpu load classifier thresholds", TestCpuLoadClassifier),
    ("Tray animation speed follows CPU usage", TestTrayAnimationSpeed),
    ("Tray icon follows theme color", TestTrayIconThemeColor),
    ("App category classifier matches process, path, and name", TestAppCategoryClassifier),
    ("Atomic file writer creates and replaces files", TestAtomicFileWriter),
    ("Settings store creates defaults", TestSettingsStoreCreatesDefaults),
    ("Settings store backs up corrupt JSON", TestSettingsStoreCorruptJson),
    ("Usage store round-trips daily usage", TestUsageStoreRoundTrip)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(ex);
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} test(s) failed.");
    return 1;
}

Console.WriteLine($"{tests.Length} test(s) passed.");
return 0;

static Task TestCpuLoadClassifier()
{
    AssertEqual(CpuLoadLevel.Low, CpuLoadClassifier.Classify(0));
    AssertEqual(CpuLoadLevel.Low, CpuLoadClassifier.Classify(19.99));
    AssertEqual(CpuLoadLevel.Normal, CpuLoadClassifier.Classify(20));
    AssertEqual(CpuLoadLevel.Normal, CpuLoadClassifier.Classify(59.99));
    AssertEqual(CpuLoadLevel.High, CpuLoadClassifier.Classify(60));
    AssertEqual(CpuLoadLevel.High, CpuLoadClassifier.Classify(84.99));
    AssertEqual(CpuLoadLevel.VeryHigh, CpuLoadClassifier.Classify(85));
    AssertEqual(CpuLoadLevel.VeryHigh, CpuLoadClassifier.Classify(100));
    return Task.CompletedTask;
}

static Task TestTrayAnimationSpeed()
{
    AssertEqual(TimeSpan.FromMilliseconds(500), TrayAnimationSpeed.CalculateInterval(0));
    AssertEqual(TimeSpan.FromMilliseconds(500), TrayAnimationSpeed.CalculateInterval(5));
    AssertEqual(TimeSpan.FromMilliseconds(250), TrayAnimationSpeed.CalculateInterval(10));
    AssertEqual(TimeSpan.FromMilliseconds(50), TrayAnimationSpeed.CalculateInterval(50));
    AssertEqual(TimeSpan.FromMilliseconds(25), TrayAnimationSpeed.CalculateInterval(100));
    AssertEqual(TimeSpan.FromMilliseconds(900), TrayAnimationSpeed.CalculateInterval(100, isPaused: true));
    AssertEqual(1d, TrayAnimationSpeed.CalculateSpeedMultiplier(0));
    AssertEqual(2d, TrayAnimationSpeed.CalculateSpeedMultiplier(10));
    AssertEqual(20d, TrayAnimationSpeed.CalculateSpeedMultiplier(100));
    return Task.CompletedTask;
}

static Task TestTrayIconThemeColor()
{
    AssertIconIsMostly("cat", useDarkMode: true, shouldBeLight: true);
    AssertIconIsMostly("cat", useDarkMode: false, shouldBeLight: false);
    AssertIconIsMostly("dog", useDarkMode: true, shouldBeLight: true);
    AssertIconIsMostly("dog", useDarkMode: false, shouldBeLight: false);
    return Task.CompletedTask;
}

static Task TestAppCategoryClassifier()
{
    var classifier = new AppCategoryClassifier(new AppCategoryRuleSet
    {
        Rules =
        [
            new AppCategoryRule
            {
                Category = AppCategory.Work,
                ProcessNames = ["code"],
                PathKeywords = ["jetbrains"],
                NameKeywords = ["figma"]
            },
            new AppCategoryRule
            {
                Category = AppCategory.Social,
                ProcessNames = ["wechat"]
            }
        ]
    });

    AssertEqual(AppCategory.Work, classifier.Classify(new ForegroundAppInfo { ProcessName = "Code" }));
    AssertEqual(AppCategory.Work, classifier.Classify(new ForegroundAppInfo { ExecutablePath = @"C:\Tools\JetBrains\idea64.exe" }));
    AssertEqual(AppCategory.Work, classifier.Classify(new ForegroundAppInfo { Name = "Figma" }));
    AssertEqual(AppCategory.Social, classifier.Classify(new ForegroundAppInfo { ProcessName = "WeChat" }));
    AssertEqual(AppCategory.Other, classifier.Classify(new ForegroundAppInfo { ProcessName = "unknown" }));
    return Task.CompletedTask;
}

static async Task TestAtomicFileWriter()
{
    var directory = CreateTempDirectory();
    var file = Path.Combine(directory, "data.txt");

    await AtomicFileWriter.WriteTextAsync(file, "first");
    AssertEqual("first", await File.ReadAllTextAsync(file));

    await AtomicFileWriter.WriteTextAsync(file, "second");
    AssertEqual("second", await File.ReadAllTextAsync(file));
    AssertFalse(File.Exists($"{file}.tmp"), "Temporary file should not remain after successful write.");
}

static async Task TestSettingsStoreCreatesDefaults()
{
    var paths = new AppDataPaths(CreateTempDirectory());
    var store = new SettingsStore(paths);

    var settings = await store.LoadAsync();

    AssertTrue(File.Exists(paths.SettingsFile), "settings.json should be created.");
    AssertTrue(settings.ReminderEnabled, "Reminder should be enabled by default.");
    AssertEqual("cat", settings.ReminderCharacter);
}

static async Task TestSettingsStoreCorruptJson()
{
    var paths = new AppDataPaths(CreateTempDirectory());
    paths.EnsureCreated();
    await File.WriteAllTextAsync(paths.SettingsFile, "{not-json");

    var store = new SettingsStore(paths);
    var settings = await store.LoadAsync();

    AssertEqual("cat", settings.ReminderCharacter);
    AssertTrue(File.Exists($"{paths.SettingsFile}.corrupt"), "Corrupt settings should be backed up.");
}

static async Task TestUsageStoreRoundTrip()
{
    var paths = new AppDataPaths(CreateTempDirectory());
    var store = new UsageStore(paths);
    var date = new DateOnly(2026, 4, 28);
    var usage = new DailyUsage
    {
        Date = date,
        TotalActiveSeconds = 120,
        ContinuousActiveSeconds = 60
    };
    usage.Apps["code"] = new AppUsage
    {
        Name = "Visual Studio Code",
        ProcessName = "Code",
        ActiveSeconds = 120,
        Category = AppCategory.Work,
        FirstUsedAt = DateTimeOffset.Parse("2026-04-28T10:00:00+08:00"),
        LastUsedAt = DateTimeOffset.Parse("2026-04-28T10:02:00+08:00")
    };

    await store.SaveAsync(usage);
    var loaded = await store.LoadAsync(date);

    AssertEqual(120, loaded.TotalActiveSeconds);
    AssertEqual(60, loaded.ContinuousActiveSeconds);
    AssertEqual(AppCategory.Work, loaded.Apps["code"].Category);
}

static string CreateTempDirectory()
{
    var directory = Path.Combine(Path.GetTempPath(), "ScreenTime.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertIconIsMostly(string reminderCharacter, bool useDarkMode, bool shouldBeLight)
{
    using var icon = TrayReminderIconFactory.CreateFrames(reminderCharacter, CpuLoadLevel.Low, useDarkMode)[0];
    using var bitmap = icon.ToBitmap();
    var total = 0;
    var count = 0;

    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.A == 0)
            {
                continue;
            }

            total += pixel.R + pixel.G + pixel.B;
            count++;
        }
    }

    AssertTrue(count > 0, "Generated tray icon should contain visible pixels.");
    var averageChannel = total / (count * 3);
    if (shouldBeLight)
    {
        AssertTrue(averageChannel > 180, $"Expected a light icon, got average channel {averageChannel}.");
    }
    else
    {
        AssertTrue(averageChannel < 80, $"Expected a dark icon, got average channel {averageChannel}.");
    }
}
