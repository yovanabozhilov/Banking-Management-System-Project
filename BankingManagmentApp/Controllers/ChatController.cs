using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Identity;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;

namespace BankingManagmentApp.Controllers
{
    [Route("chat")]
    public class ChatController : Controller
    {
        private const string SessionKey = "chatHistory";
        private readonly AiChatService _ai;
        private readonly UserManager<Customers> _userManager;

        public ChatController(AiChatService ai, UserManager<Customers> userManager)
        {
            _ai = ai;
            _userManager = userManager;
        }

        [HttpGet("")]
        public IActionResult Index() => View();

        // ---------- Non-streaming JSON endpoint ----------
        // POST /chat/send
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] ChatRequestDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto?.Message))
                return BadRequest(new { reply = "Message cannot be empty." });

            // Load history from session
            var history = LoadHistory();

            // Track the new user message in session history
            history.Add(new ChatMessage(ChatRole.User, dto.Message.Trim()));
            TrimHistory(history);

            // Current user (may be null -> no personalized tools)
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            var firstName = user?.FirstName ?? user?.UserName;

            // Ask the AI (RAG via TemplateAnswer + Tools later)
            var reply = await _ai.SendAsync(dto.Message, userId, firstName, history, ct);

            // Save assistant reply to history
            history.Add(new ChatMessage(ChatRole.Assistant, reply));
            SaveHistory(history);

            return Ok(new { reply });
        }

        // ---------- Streaming endpoint (Server-Sent Events) ----------
        // GET /chat/stream?prompt=...
        [HttpGet("stream")]
        public async Task Stream([FromQuery] string prompt, CancellationToken ct)
        {
            Response.Headers["Content-Type"] = "text/event-stream";

            if (string.IsNullOrWhiteSpace(prompt))
            {
                await Response.WriteAsync("data: Message cannot be empty.\n\n", ct);
                await Response.Body.FlushAsync(ct);
                return;
            }

            // Load and update history with the new user message
            var history = LoadHistory();
            history.Add(new ChatMessage(ChatRole.User, prompt.Trim()));
            TrimHistory(history);

            // Current user context
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            var firstName = user?.FirstName ?? user?.UserName;

            // Stream AI reply (RAG via TemplateAnswer)
            string assistantReply = "";
            await foreach (var chunk in _ai.StreamAsync(prompt, userId, firstName, history, ct))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    assistantReply += chunk;
                    await Response.WriteAsync($"data: {chunk.Replace("\n", "\\n")}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }

            // Persist assistant reply in history
            history.Add(new ChatMessage(ChatRole.Assistant, assistantReply));
            SaveHistory(history);
        }

        // ---------- Helpers ----------
        private List<ChatMessage> LoadHistory()
        {
            var json = HttpContext.Session.GetString(SessionKey);
            if (string.IsNullOrEmpty(json)) return new List<ChatMessage>();
            try
            {
                return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }

        private void SaveHistory(List<ChatMessage> history)
        {
            var json = JsonSerializer.Serialize(history);
            HttpContext.Session.SetString(SessionKey, json);
        }

        // Keep the session payload bounded
        private static void TrimHistory(List<ChatMessage> history, int maxMessages = 30)
        {
            if (history.Count > maxMessages)
            {
                var skip = history.Count - maxMessages;
                history.RemoveRange(0, skip);
            }
        }
    }

    public record ChatRequestDto(string? Message);
}
