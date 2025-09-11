using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace BankingManagmentApp.Services;

public class AiChatService
{
    private readonly IChatClient _chatClient;

    public AiChatService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    // Non-streaming: single reply
    public async Task<string> SendMessageAsync(IList<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatResponse = await _chatClient.GetResponseAsync(history, cancellationToken: cancellationToken);
            var last = chatResponse.Messages?.LastOrDefault();
            return last?.Text ?? "[No response from AI]";
        }
        catch
        {
            return "[AI service unavailable]";
        }
    }

    // Streaming: yields partial text updates
    public async IAsyncEnumerable<string> StreamMessageAsync(
        IList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _chatClient.GetStreamingResponseAsync(history, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}