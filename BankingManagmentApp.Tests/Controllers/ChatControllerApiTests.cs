using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BankingManagmentApp; 

namespace BankingManagmentApp.Tests.Controllers
{
    public class ChatControllerApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ChatControllerApiTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
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

            response.EnsureSuccessStatusCode();
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
