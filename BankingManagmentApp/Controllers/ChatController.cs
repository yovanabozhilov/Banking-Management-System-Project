using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Text.Json;
using BankingManagmentApp.Services;

namespace BankingManagmentApp.Controllers
{
    [Route("chat")]
    public class ChatController : Controller
    {
        private readonly AiChatService _ai;

        private static readonly Dictionary<string, string> FaqAnswers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Create Account", "You can create a new account by clicking 'Register' and providing your details." },
            { "Forgot Password", "Click 'Forgot Password', enter your email, and follow the reset instructions." },
            { "Account Security", "Your account is protected with 2FA, encrypted transactions, and secure passwords." },
            { "Update Profile", "Go to Profile → Edit Profile to update your phone number, city, or personal details." },
            { "Manage Security", "Allows you to change password, enable 2FA, and review login activity." },
            { "No Transactions", "You haven’t made any transactions yet; deposits, transfers, or payments will appear here." },
            { "Apply Loan", "Go to Loans → Apply for Loan, select type, and submit required documents." },
            { "Improve Credit", "Pay loans/bills on time, keep utilization low, avoid many new loans, maintain healthy transaction history." },
            { "Loan Documents", "Typically need ID, proof of income, and address verification; varies by loan type." },
            { "Early Repayment", "Yes, you can repay loans early; some may have a small early repayment fee." }
        };

        public ChatController(AiChatService ai) => _ai = ai;

        // POST /chat/send
        [HttpPost("send")]
        public IActionResult Send([FromBody] ChatRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { reply = "Message cannot be empty." });

            var key = dto.Message.Trim();

            // Match ignoring case and whitespace
            var match = FaqAnswers.Keys
                .FirstOrDefault(k => string.Equals(k.Trim(), key, StringComparison.OrdinalIgnoreCase));

            string reply = match != null ? FaqAnswers[match] : "Sorry, I don’t have an answer for that yet.";

            return Ok(new { reply });
        }

        // GET /chat/stream?prompt=...  - server-sent-events streaming endpoint
        [HttpGet("stream")]
        public async Task Stream([FromQuery] string prompt, CancellationToken ct)
        {
            Response.Headers["Content-Type"] = "text/event-stream";

            // Load history from session
            var json = HttpContext.Session.GetString("chatHistory");
            var history = string.IsNullOrEmpty(json)
                ? new List<ChatMessage>()
                : JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();

            if (!history.Any(m => m.Role == ChatRole.System))
            {
                history.Insert(0, new ChatMessage(ChatRole.System, "You are a helpful banking assistant. Keep answers concise."));
            }

            history.Add(new ChatMessage(ChatRole.User, prompt));

            string assistantReply = "";

            await foreach (var chunk in _ai.StreamMessageAsync(history, ct))
            {
                assistantReply += chunk;
                // SSE format: data: <text>\n\n
                await Response.WriteAsync($"data: {chunk.Replace("\n", "\\n")}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            history.Add(new ChatMessage(ChatRole.Assistant, assistantReply));
            HttpContext.Session.SetString("chatHistory", JsonSerializer.Serialize(history));
        }
    }

    public record ChatRequestDto(string Message);
}