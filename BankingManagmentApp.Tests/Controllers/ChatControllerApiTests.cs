using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BankingManagmentApp; // <- this resolves Program

namespace BankingManagmentApp.Tests.Controllers
{
    public class ChatControllerApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ChatControllerApiTests(WebApplicationFactory<Program> factory)
        {
            // Create client with test server
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Send_ReturnsBadRequest_WhenMessageIsEmpty()
        {
            // Arrange
            var dto = new { Message = "" };

            // Act
            var response = await _client.PostAsJsonAsync("/chat/send", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Message cannot be empty", content);
        }

        [Fact]
        public async Task Send_ReturnsOk_WhenMessageIsProvided()
        {
            // Arrange
            var dto = new { Message = "Hello AI!" };

            // Act
            var response = await _client.PostAsJsonAsync("/chat/send", dto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("reply", json);
        }

        [Fact]
        public async Task Stream_ReturnsBadRequestMessage_WhenPromptIsEmpty()
        {
            // Act
            var response = await _client.GetAsync("/chat/stream?prompt=");

            // Assert
            response.EnsureSuccessStatusCode(); // SSE still returns 200
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Message cannot be empty", content);
        }

        [Fact]
        public async Task Index_ReturnsHtmlPage()
        {
            // Act
            var response = await _client.GetAsync("/chat");

            // Assert
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<html", html.ToLower()); // crude check
        }
    }
}
