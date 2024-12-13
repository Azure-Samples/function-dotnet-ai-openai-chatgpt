using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenAI.Assistants;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace chatSample;

/// <summary>
/// The ChatBot sample allows you to create chat bots with a specified set of initial instructions.
/// </summary>
public class ChatBot
{
    const string DefaultChatStorageConnectionSetting = "AzureWebJobsStorage";
    const string DefaultCollectionName = "ChatState";

    private readonly ILogger<ChatBot> _logger;

    public ChatBot(ILogger<ChatBot> logger)
    {
        _logger = logger;
    }

    [Function("chat")]
    public CreateChatBotOutput CreateAssistant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "chats/{assistantId}")]
            HttpRequestData req,
        string assistantId
    )
    {
        var responseJson = new { assistantId };

        string instructions = """
            Don't make assumptions about what values to plug into functions.
            Ask for clarification if a user request is ambiguous.
            """;

        return new CreateChatBotOutput
        {
            HttpResponse = new OkObjectResult(responseJson),
            ChatBotCreateRequest = new AssistantCreateRequest(assistantId, instructions)
            {
                ChatStorageConnectionSetting = DefaultChatStorageConnectionSetting,
                CollectionName = DefaultCollectionName,
            },
        };
    }

    public class PostResponseOutput
    {
        [HttpResult]
        public IActionResult? HttpResponse { get; set; }
    }

    [Function("chatQuery")]
    public PostResponseOutput ChatQuery(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chats/{assistantId}")]
            HttpRequestData req,
        string assistantId,
        [AssistantPostInput(
            "{assistantId}",
            "{prompt}",
            Model = "%AZURE_OPENAI_CHATGPT_DEPLOYMENT%",
            ChatStorageConnectionSetting = DefaultChatStorageConnectionSetting,
            CollectionName = DefaultCollectionName
        )]
            AssistantState state
    )
    {
        // Send response to client in expected format, including assistantId
        var _answer = new AnswerResponse(
            new string[] { },
            state.RecentMessages.LastOrDefault()?.Content ?? "No response returned.",
            ""
        );

        return new PostResponseOutput { HttpResponse = new OkObjectResult(_answer) };
    }

    [Function(nameof(GetChatState))]
    public IActionResult GetChatState(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "chats/{assistantId}")]
            HttpRequestData req,
        string assistantId,
        [AssistantQueryInput(
            "{assistantId}",
            TimestampUtc = "{Query.timestampUTC}",
            ChatStorageConnectionSetting = DefaultChatStorageConnectionSetting,
            CollectionName = DefaultCollectionName
        )]
            AssistantState state
    )
    {
        // Returns the last message from the history table which will be the latest answer to the last question
        var _answer = new AnswerResponse(
            new string[] { },
            state.RecentMessages.LastOrDefault()?.Content ?? "No response returned.",
            ""
        );

        return new OkObjectResult(_answer);
    }

    public class CreateChatBotOutput
    {
        [AssistantCreateOutput()]
        public AssistantCreateRequest? ChatBotCreateRequest { get; set; }

        [HttpResult]
        public IActionResult? HttpResponse { get; set; }
    }

    public record AnswerResponse(
        [property: JsonPropertyName("data_points")] string[] DataPoints,
        [property: JsonPropertyName("answer")] string Answer,
        [property: JsonPropertyName("thoughts")] string thoughts
    ) { };
}
