using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace PlaywrightTesting;

[TestFixture]
[NonParallelizable]
public class LambdaTestTests : PlaywrightTest
{
    private const string SuccessText = "You logged into a secure area!";
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    [SetUp]
    public async Task SetUp()
    {
        var username = GetRequiredEnvironmentVariable("LT_USERNAME");
        var accessKey = GetRequiredEnvironmentVariable("LT_ACCESS_KEY");
        var testName = TestContext.CurrentContext.Test.Name;

        try
        {
            _browser = await Playwright.Chromium.ConnectAsync(BuildConnectionUrl(username, accessKey, testName));
            _context = await _browser.NewContextAsync(GetContextOptions(testName));
            _page = await _context.NewPageAsync();
        }
        catch
        {
            await CloseResourcesAsync();
            throw;
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            if (_page is not null)
            {
                var result = TestContext.CurrentContext.Result;
                var passed = result.Outcome.Status == TestStatus.Passed;
                var remark = passed
                    ? TestContext.CurrentContext.Test.Name switch
                    {
                        nameof(IPhoneTest) => "Login successful on iPhone",
                        nameof(IPadTest) => "Login successful on iPad",
                        _ => "Login successful"
                    }
                    : result.Message ?? "Test failed";
                await SetTestStatusAsync(passed ? "passed" : "failed", remark, _page);
            }
        }
        finally
        {
            await CloseResourcesAsync();
        }
    }

    [Test]
    [Category("Desktop")]
    public async Task SingleTest() => await VerifyLoginAsync();

    [Test]
    [Category("Mobile")]
    public async Task IPhoneTest() => await VerifyLoginAsync();

    [Test]
    [Category("Mobile")]
    public async Task IPadTest() => await VerifyLoginAsync();

    private async Task VerifyLoginAsync()
    {
        var page = _page ?? throw new InvalidOperationException("The browser page was not initialized.");

        await page.GotoAsync("https://the-internet.herokuapp.com/login");
        await page.FillAsync("#username", "tomsmith");
        await page.FillAsync("#password", "SuperSecretPassword!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForSelectorAsync(".flash.success");

        var successMessage = await page.Locator(".flash.success").TextContentAsync();
        Assert.That(successMessage, Does.Contain(SuccessText));
    }

    private BrowserNewContextOptions? GetContextOptions(string testName) => testName switch
    {
        nameof(IPhoneTest) => Playwright.Devices["iPhone 13"],
        nameof(IPadTest) => Playwright.Devices["iPad Pro 11 landscape"],
        _ => null
    };

    private static string BuildConnectionUrl(string username, string accessKey, string testName)
    {
        var sessionName = testName switch
        {
            nameof(IPhoneTest) => "Playwright Login Test on iPhone",
            nameof(IPadTest) => "Playwright Login Test on iPad",
            _ => "Playwright Login Test"
        };
        var capabilities = new Dictionary<string, object?>
        {
            ["browserName"] = "Chrome",
            ["browserVersion"] = "latest",
            ["LT:Options"] = new Dictionary<string, object?>
            {
                ["name"] = sessionName,
                ["build"] = "Playwright C-Sharp tests on Hyperexecute",
                ["platform"] = Environment.GetEnvironmentVariable("HYPEREXECUTE_PLATFORM"),
                ["user"] = username,
                ["accessKey"] = accessKey
            }
        };

        return "wss://cdp.lambdatest.com/playwright?capabilities="
            + Uri.EscapeDataString(JsonSerializer.Serialize(capabilities));
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The {name} environment variable is required.");

    private static Task SetTestStatusAsync(string status, string remark, IPage page)
    {
        var action = "lambdatest_action: " + JsonSerializer.Serialize(new
        {
            action = "setTestStatus",
            arguments = new { status, remark }
        });

        return page.EvaluateAsync("_ => {}", action);
    }

    private async Task CloseResourcesAsync()
    {
        try
        {
            if (_page is not null)
            {
                await _page.CloseAsync();
            }
        }
        finally
        {
            try
            {
                if (_context is not null)
                {
                    await _context.CloseAsync();
                }
            }
            finally
            {
                if (_browser is not null)
                {
                    await _browser.CloseAsync();
                }

                _page = null;
                _context = null;
                _browser = null;
            }
        }
    }
}
