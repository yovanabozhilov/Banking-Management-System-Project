using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace BankingManagmentApp.Services
{
    public class AiChatService
    {
        private readonly IChatClient _chatClient;
        private readonly KnowledgeBaseService _kb;
        private readonly ChatTools _tools;

        public AiChatService(IChatClient chatClient, KnowledgeBaseService kb, ChatTools tools)
        {
            _chatClient = chatClient;
            _kb = kb;
            _tools = tools;
        }

        private ChatMessage SystemMessage(string? userFirstName) => new(
            ChatRole.System,
            $@"You are a banking virtual assistant for Experian Workshop 'Banking Management System'.

Safety & Compliance:
- Never disclose sensitive data or internal implementation.
- For personalized info you MUST rely only on provided tool results; do not guess.
- If the user is not authenticated, ask them to log in.
- Do not accept card numbers, PINs, OTPs, or full IBANs in chat. If provided, refuse and instruct secure channels.
- Be concise, factual, and professional. If unsure, say so.

Capabilities:
1) Answer FAQs using retrieved bank knowledge (snippets provided in-context).
2) Use the provided tools to answer personal questions like balance, transactions, and loan status when the user is authenticated.
3) If neither applies, give a helpful, bounded, generic answer.

Tone: friendly, precise, and compliant. Address the user by name if provided: {userFirstName ?? "Customer"}.");

        // ========== PUBLIC ==========
        public async Task<string> SendAsync(
            string userInput,
            string? userId,
            string? userFirstName,
            IList<ChatMessage>? prior = null,
            CancellationToken ct = default)
        {
            try
            {
                // 1) Personal queries -> answer via tools (no model call)
                var toolAnswer = await TryAnswerWithToolsAsync(userInput, userId, ct);
                if (toolAnswer is not null)
                    return toolAnswer;

                // 2) Otherwise ask the model with KB context
                var messages = await BuildMessagesAsync(userInput, userFirstName, prior, ct);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(20));

                var chatResponse = await _chatClient.GetResponseAsync(messages, cancellationToken: cts.Token);
                return chatResponse.Messages?.LastOrDefault()?.Text ?? "[no response]";
            }
            catch
            {
                return "[AI service unavailable]";
            }
        }

        public async IAsyncEnumerable<string> StreamAsync(
            string userInput,
            string? userId,
            string? userFirstName,
            IList<ChatMessage>? prior = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var toolAnswer = await TryAnswerWithToolsAsync(userInput, userId, ct);
            if (toolAnswer is not null)
            {
                yield return toolAnswer;
                yield break;
            }

            var messages = await BuildMessagesAsync(userInput, userFirstName, prior, ct);
            await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    yield return update.Text;
            }
        }

        public async Task<string> SendMessageAsync(IList<ChatMessage> history, CancellationToken cancellationToken = default)
        {
            try
            {
                var chatResponse = await _chatClient.GetResponseAsync(history, cancellationToken: cancellationToken);
                return chatResponse.Messages?.LastOrDefault()?.Text ?? "[No response from AI]";
            }
            catch
            {
                return "[AI service unavailable]";
            }
        }

        public async IAsyncEnumerable<string> StreamMessageAsync(IList<ChatMessage> history, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in _chatClient.GetStreamingResponseAsync(history, cancellationToken: cancellationToken))
                if (!string.IsNullOrEmpty(update.Text)) yield return update.Text;
        }

        // ========== INTERNAL ==========
        private async Task<IList<ChatMessage>> BuildMessagesAsync(
            string userInput,
            string? userFirstName,
            IList<ChatMessage>? prior,
            CancellationToken ct)
        {
            var history = prior ?? new List<ChatMessage>();

            // RAG from TemplateAnswer (your KB replacement)
            var hits = await _kb.SearchAsync(userInput, top: 3, ct);
            var kbContext = string.Join(
                "\n---\n",
                hits.Select(h => $"Keyword(s): {h.Keyword}\nAnswer:\n{h.AnswerText}")
            );

            var messages = new List<ChatMessage>
            {
                SystemMessage(userFirstName),
                new ChatMessage(ChatRole.User, $"FAQ/Policy context:\n{kbContext}"),
                new ChatMessage(ChatRole.User, userInput)
            };

            foreach (var old in history.TakeLast(6))
                messages.Insert(1, old);

            return messages;
        }

        // ========== TOOL INTENTS ==========
        private enum Intent { None, Balance, RecentTransactions, LoanStatus }

        private static Intent ParseIntent(string text, out int n)
        {
            n = 0;
            if (string.IsNullOrWhiteSpace(text)) return Intent.None;
            var s = text.ToLowerInvariant();

            // Recent transactions
            if (s.Contains("transaction"))
            {
                // explicit number: "last 3", "recent 2", "past 5"
                var m = Regex.Match(s, @"\b(last|recent|past)\s+(\d+)\b");
                if (m.Success && int.TryParse(m.Groups[2].Value, out var num) && num > 0)
                    n = num;

                // singular: "last transaction"
                if (n <= 0 && Regex.IsMatch(s, @"\blast\s*transaction\b"))
                    n = 1;

                if (n <= 0) n = 5; // sensible default
                return Intent.RecentTransactions;
            }

            // Balance
            if (s.Contains("balance") || s.Contains("how much money") || s.Contains("my funds"))
                return Intent.Balance;

            // Loan status
            if ((s.Contains("loan") && s.Contains("status")) || s.Contains("application status"))
                return Intent.LoanStatus;

            return Intent.None;
        }

        private async Task<string?> TryAnswerWithToolsAsync(string userInput, string? userId, CancellationToken ct)
        {
            var intent = ParseIntent(userInput, out var n);
            if (intent == Intent.None) return null;

            if (string.IsNullOrEmpty(userId))
                return "Please log in to view your personal banking details.";

            try
            {
                switch (intent)
                {
                    case Intent.Balance:
                    {
                        var balance = await _tools.GetBalanceAsync(userId, ct);
                        return $"Your total balance across accounts is {balance:0.00}.";
                    }
                    case Intent.RecentTransactions:
                    {
                        var txns = await _tools.GetRecentTransactionsAsync(userId, count: n, ct);
                        if (txns.Count == 0)
                            return "You have no recent transactions.";

                        var lines = txns.Select(t =>
                        {
                            var type = t.GetType();
                            var id = type.GetProperty("Id")?.GetValue(t);
                            var date = type.GetProperty("Date")?.GetValue(t) as DateTime?;
                            var amount = type.GetProperty("Amount")?.GetValue(t) as decimal? ?? 0m;
                            var kind = type.GetProperty("Type")?.GetValue(t)?.ToString();
                            var desc = type.GetProperty("Description")?.GetValue(t)?.ToString();
                            var iban = type.GetProperty("AccountIban")?.GetValue(t)?.ToString();

                            var dateStr = date?.ToString("yyyy-MM-dd") ?? "-";
                            return $"- #{id} | {dateStr} | {kind} | {amount:0.00} | {desc} | {iban}";
                        });

                        return $"Here {(txns.Count == 1 ? "is your last transaction" : $"are your last {txns.Count} transactions")}:\n{string.Join("\n", lines)}";
                    }
                    case Intent.LoanStatus:
                    {
                        var status = await _tools.GetLoanStatusAsync(userId, ct);
                        return $"Loan status: {status}";
                    }
                }
            }
            catch
            {
                // If DB/tool fails, give a clear message instead of falling into the AI call.
                return "Sorry, I couldn’t fetch your personal data just now. Please try again.";
            }

            return null;
        }
    }
}
