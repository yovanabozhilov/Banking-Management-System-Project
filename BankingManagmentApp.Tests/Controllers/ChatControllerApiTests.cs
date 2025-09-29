using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BankingManagmentApp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
                               ILoggerFactory logger,
                               System.Text.Encodings.Web.UrlEncoder encoder,
                               ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Name, "testuser")
            }, "Test");

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class ChatControllerApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ChatControllerApiTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                    services.PostConfigureAll<AuthenticationOptions>(opt =>
                    {
                        opt.DefaultAuthenticateScheme = "Test";
                        opt.DefaultChallengeScheme = "Test";
                        opt.DefaultScheme = "Test";
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task Send_ReturnsBadRequest_WhenMessageIsEmpty()
        {
            var dto = new { Message = "" };

            var response = await _client.PostAsJsonAsync("/chat/send", dto);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Message cannot be empty", content);
        }

        [Fact]
        public async Task Send_ReturnsOk_WhenMessageIsProvided()
        {
            var dto = new { Message = "Hello AI!" };

            var response = await _client.PostAsJsonAsync("/chat/send", dto);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("reply", json);
        }

        [Fact]
        public async Task Stream_ReturnsBadRequestMessage_WhenPromptIsEmpty()
        {
            var response = await _client.GetAsync("/chat/stream?prompt=");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Message cannot be empty", content);
        }

        [Fact]
        public async Task Index_ReturnsHtmlPage()
        {
            var response = await _client.GetAsync("/chat");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<html", html.ToLower());
        }
    }
}
