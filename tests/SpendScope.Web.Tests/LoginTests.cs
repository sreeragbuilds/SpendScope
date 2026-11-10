using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SpendScope.Web.Pages.Auth;

namespace SpendScope.Web.Tests;

/// <summary>Spec 19.12: Login shows a generic error on 401 and a lockout message on 423.</summary>
public sealed class LoginTests : WebTestContext
{
    private IRenderedComponent<Login> RenderLogin()
    {
        AddAuthorization();
        return Render<Login>();
    }

    private static void SubmitCredentials(IRenderedComponent<Login> cut)
    {
        var inputs = cut.FindAll("input");
        inputs[0].Input("priya@example.com");
        inputs[1].Input("Wr0ng!Password");
        cut.Find("form").Submit();
    }

    [Fact]
    public void Shows_a_generic_message_on_401()
    {
        UseServer(_ => Problem(HttpStatusCode.Unauthorized, """{"title":"Email or password is incorrect.","status":401,"errorCode":"invalid_credentials"}"""));
        var cut = RenderLogin();

        SubmitCredentials(cut);

        cut.WaitForAssertion(() => Assert.Contains("Email or password is incorrect.", cut.Find("[data-testid=login-error]").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("not found", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shows_minutes_until_unlock_on_423()
    {
        UseServer(_ => Problem((HttpStatusCode)423, """{"title":"Locked","status":423,"errorCode":"account_locked"}""", TimeSpan.FromSeconds(290)));
        var cut = RenderLogin();

        SubmitCredentials(cut);

        cut.WaitForAssertion(() => Assert.Contains("Try again in 5 minute(s)", cut.Find("[data-testid=login-error]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Successful_login_navigates_to_the_return_url()
    {
        UseServer(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"accessToken":"a.b.c","refreshToken":"r","expiresIn":900,"tokenType":"Bearer"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        AddAuthorization(); // register everything before the first service is resolved
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("/login?returnUrl=%2Fexpenses");
        var cut = Render<Login>();

        SubmitCredentials(cut);

        cut.WaitForAssertion(() => Assert.EndsWith("/expenses", navigation.Uri, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/expenses", "/expenses")]
    [InlineData("/admin/users?page=2", "/admin/users?page=2")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    public void Return_url_is_limited_to_this_site(string? returnUrl, string expected) =>
        Assert.Equal(expected, Login.SafeReturnUrl(returnUrl));
}
