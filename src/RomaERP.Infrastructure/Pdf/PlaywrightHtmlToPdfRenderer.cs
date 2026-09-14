using Microsoft.Playwright;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Pdf;

/// <summary>Renders HTML to PDF via a headless Chromium instance (Microsoft.Playwright). Registered as a
/// singleton — launching a browser takes ~1-2 seconds, so it's started once and reused across requests, each
/// request getting its own short-lived page/tab. Requires the Chromium browser to be present on disk (see the
/// API Dockerfile's `playwright install chromium` step, or PLAYWRIGHT_BROWSERS_PATH pointing at one already
/// installed) — this does not download a browser itself.</summary>
public class PlaywrightHtmlToPdfRenderer : IHtmlToPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string? _executablePathOverride;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightHtmlToPdfRenderer() { }

    /// <summary>Test-only escape hatch: lets a test point at a specific Chromium binary instead of relying on
    /// Playwright's own revision-matched auto-resolution — useful when a pre-installed browser on disk doesn't
    /// match this NuGet package's expected revision (as in this project's own dev sandbox). Production code
    /// should use the parameterless constructor and a `playwright install chromium` step at deploy time.</summary>
    public PlaywrightHtmlToPdfRenderer(string executablePathOverride)
    {
        _executablePathOverride = executablePathOverride;
    }

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct = default)
    {
        var browser = await GetBrowserAsync(ct);
        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });
            return await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,
                Margin = new Margin { Top = "12mm", Bottom = "12mm", Left = "10mm", Right = "10mm" },
            });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null)
            return _browser;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_browser is not null)
                return _browser;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                ExecutablePath = _executablePathOverride,
                Args = new[] { "--no-sandbox" }, // running as root inside the API container
            });
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
