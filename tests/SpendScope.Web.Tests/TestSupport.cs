using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using SpendScope.Web.Services.Auth;
using SpendScope.Web.Services.Browser;
using SpendScope.Web.Services.Charts;

namespace SpendScope.Web.Tests;

/// <summary>Answers every request with a canned response and remembers the requests.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") };
}

internal sealed class RecordingChartInterop(Microsoft.JSInterop.IJSRuntime js) : ChartInterop(js)
{
    public List<(string CanvasId, IReadOnlyList<ChartSlice> Slices)> Doughnuts { get; } = [];

    public List<string> Destroyed { get; } = [];

    public override ValueTask RenderDoughnutAsync(string canvasId, IReadOnlyList<ChartSlice> slices, string currency)
    {
        Doughnuts.Add((canvasId, slices));
        return ValueTask.CompletedTask;
    }

    public override ValueTask DestroyAsync(string canvasId)
    {
        Destroyed.Add(canvasId);
        return ValueTask.CompletedTask;
    }
}

public abstract class WebTestContext : BunitContext
{
    protected WebTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<BrowserStorage>();
        Services.AddSingleton<TokenStore>();
    }

    /// <summary>Wires AuthSession to a stub server that answers with <paramref name="respond"/>.</summary>
    internal StubHandler UseServer(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        Services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(handler));
        Services.AddSingleton<AuthSession>();
        return handler;
    }

    internal static HttpResponseMessage Problem(HttpStatusCode status, string json, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/problem+json"),
        };
        if (retryAfter is { } wait)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
        }

        return response;
    }
}
