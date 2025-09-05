using System;
using Azure.AI.OpenAI;
using BankingManagmentApp.Models;

namespace BankingManagmentApp.Services
{
    public class ChatService
    {
        private readonly OpenAIClient _client;
        private readonly ChatHistoryRepository _chatHistoryRepo;
        private readonly TemplateAnswerRepository _templateRepo;

        public ChatService(
            OpenAIClient client,
            ChatHistoryRepository chatHistoryRepo,
            TemplateAnswerRepository templateRepo)
        {
            _client = client;
            _chatHistoryRepo = chatHistoryRepo;
            _templateRepo = templateRepo;
        }

        public async Task<string> GetResponseAsync(string userInput, Customers user)
        {
            // 1) Шаблонен отговор (ако има съвпадение)
            var template = _templateRepo.FindMatch(userInput);
            if (template != null)
            {
                var response = (template.AnswerText ?? string.Empty)
                    .Replace("{UserName}", user.UserName ?? "User");

                await SaveChatHistoryAsync(user.Id, userInput, response);
                return response;
            }

            // 2) AI отговор
            var options = new ChatCompletionsOptions
            {
                Messages = { new ChatMessage(ChatRole.User, userInput) }
            };

            var completionResponse = await _client.GetChatCompletionsAsync("gpt-4o-mini", options);

            // Универсален начин да вземем текста (работи в различните версии на SDK)
            var result = completionResponse.Value?.Choices?[0]?.Message?.Content?.ToString() ?? string.Empty;

            await SaveChatHistoryAsync(user.Id, userInput, result);
            return result;
        }

        // преименуван метод, за да избегнем конфликт с други "SaveHistory"
        private async Task SaveChatHistoryAsync(string userId, string userInput, string response)
        {
            await _chatHistoryRepo.AddMessageAsync(new ChatHistory
            {
                CustomerId = userId,
                Sender = "User",
                Message = userInput,
                Timestamp = DateTime.UtcNow
            });

            await _chatHistoryRepo.AddMessageAsync(new ChatHistory
            {
                CustomerId = userId,
                Sender = "Assistant",
                Message = response,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
