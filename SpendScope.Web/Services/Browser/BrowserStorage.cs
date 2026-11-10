using Microsoft.JSInterop;

namespace SpendScope.Web.Services.Browser;

/// <summary>Thin wrapper over wwwroot/js/browser.js.</summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    public ValueTask<string?> SessionGetAsync(string key) => js.InvokeAsync<string?>("spendscopeBrowser.sessionGet", key);

    public ValueTask SessionSetAsync(string key, string value) => js.InvokeVoidAsync("spendscopeBrowser.sessionSet", key, value);

    public ValueTask SessionRemoveAsync(string key) => js.InvokeVoidAsync("spendscopeBrowser.sessionRemove", key);

    public ValueTask<string?> LocalGetAsync(string key) => js.InvokeAsync<string?>("spendscopeBrowser.localGet", key);

    public ValueTask LocalSetAsync(string key, string value) => js.InvokeVoidAsync("spendscopeBrowser.localSet", key, value);

    public ValueTask<bool> PrefersDarkAsync() => js.InvokeAsync<bool>("spendscopeBrowser.prefersDark");

    public ValueTask DownloadAsync(string fileName, string contentType, byte[] content) =>
        js.InvokeVoidAsync("spendscopeBrowser.downloadFile", fileName, contentType, Convert.ToBase64String(content));
}
