<!--
---
page_type: sample
languages:
- azdeveloper
- csharp
- bicep
- html
products:
- azure
- azure-functions
- azure-pipelines
- azure-openai
- azure-cognitive-search
- ai-services
- blob-storage
- table-storage
urlFragment: function-dotnet-ai-openai-chatgpt
name: Azure Functions - Chat using Azure OpenAI (.NET 8 Function)
description: This sample shows simple ways to interact with Azure OpenAI & GPT-4 model to build an interactive using Azure Functions using [Azure Open AI Triggers and Bindings extension](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-openai?tabs=isolated-process&pivots=programming-language-python).  You can issue simple prompts and receive completions using the `ask` function, and you can send messages and perform a stateful session with a friendly ChatBot using the `chat` function.
---
-->
<!-- YAML front-matter schema: https://review.learn.microsoft.com/en-us/help/contribute/samples/process/onboarding?branch=main#supported-metadata-fields-for-readmemd -->

# Azure Functions
## Chat using Azure OpenAI (.NET 8 Function)

This sample shows simple ways to interact with Azure OpenAI & GPT-4 model to build an interactive using Azure Functions [Azure Open AI Triggers and Bindings extension](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-openai?tabs=isolated-process&pivots=programming-language-csharp).  You can issue simple prompts and receive completions using the `ask` function, and you can send messages and perform a stateful session with a friendly ChatBot using the `chats` function.  The app deploys easily to Azure Functions Flex Consumption hosting plan using `azd up`. 

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure-Samples/function-dotnet-ai-openai-chatgpt)

## Run on your local environment

### Pre-reqs
1) [.NET 8](https://www.dot.net) 
2) [Azure Functions Core Tools 4.0.6610 or higher](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v4%2Cmacos%2Ccsharp%2Cportal%2Cbash#install-the-azure-functions-core-tools)
3) [Azurite](https://github.com/Azure/Azurite)

The easiest way to install Azurite is using a Docker container or the support built into Visual Studio:
```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

4) Once you have your Azure subscription, run the following in a new terminal window to create Azure OpenAI and other resources needed:
```bash
azd provision
```

Take note of the value of `AZURE_OPENAI_ENDPOINT` which can be found in `./.azure/<env name from azd provision>/.env`.  It will look something like:
```bash
AZURE_OPENAI_ENDPOINT="https://cog-<unique string>.openai.azure.com/"
```

Alternatively you can [create an OpenAI resource](https://portal.azure.com/#create/Microsoft.CognitiveServicesTextAnalytics) in the Azure portal to get your key and endpoint. After it deploys, click Go to resource and view the Endpoint value.  You will also need to deploy a model, e.g. with name `chat` and model `gpt-4o`.

5) Add this `local.settings.json` file to the **app** folder to simplify local development.  Replace `AZURE_OPENAI_ENDPOINT` with your value from step 4.  Optionally you can choose a different model deployment in `CHAT_MODEL_DEPLOYMENT_NAME`.  This file will be gitignored to protect secrets from committing to your repo, however by default the sample uses Entra identity (user identity and mananaged identity) so it is secretless.  
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_OPENAI_ENDPOINT": "https://cog-<unique string>.openai.azure.com/",
    "CHAT_MODEL_DEPLOYMENT_NAME": "chat",
    "AzureWebJobsFeatureFlags": "EnableWorkerIndexing"
  }
}
```

## Simple Prompting with Ask Function
### Using Functions CLI
1) Open a new terminal and do the following:

```bash
cd app
func start
```

Alternatively if you have Visual Studio or Visual Studio Code, you can load the [solution file: chat.sln](chat.sln) and press F5 to Start/Run the solution.

2) Using your favorite REST client, e.g. [RestClient in VS Code](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) or curl, make a post.  [test.http](test.http) has been provided to run this quickly.   

Terminal:
```bash
curl -i -X POST http://localhost:7071/api/ask/ \
  -H "Content-Type: text/json" \
  --data-binary "@testdata.json"
```

testdata.json
```json
{
    "prompt": "Write a poem about Azure Functions.  Include two reasons why users love them."
}
```

[test.http](test.http)
```http
### Simple Ask Completion
POST http://localhost:7071/api/ask HTTP/1.1
content-type: application/json

{
    "prompt": "Tell me two most popular programming features of Azure Functions"
}

### Simple Whois Completion
GET http://localhost:7071/api/whois/Turing HTTP/1.1
```

## Stateful Interaction with Chatbot using Chat Function

We will use the [test.http](test.http) file again now focusing on the Chat function.  We need to start the chat with `chats` and send messages with `PostChat`.  We can also get state at any time with `GetChatState`.

```http
### Stateful Chatbot
### CreateChatBot
PUT http://localhost:7071/api/chats/abc123
Content-Type: application/json

{
    "name": "Sample ChatBot",
    "description": "This is a sample chatbot."
}

### PostChat
POST http://localhost:7071/api/chats/abc123
Content-Type: application/json

{
    "message": "Hello, how can I assist you today?"
}

### PostChat
POST http://localhost:7071/api/chats/abc123
Content-Type: application/json

{
    "message": "Need help with directions from Redmond to SeaTac?"
}

### GetChatState
GET http://localhost:7071/api/chats/abc123?timestampUTC=2024-01-15T22:00:00
Content-Type: application/json
```

## Deploy to Azure

The easiest way to deploy this app is using the [Azure Developer CLI](https://aka.ms/azd).  If you open this repo in GitHub CodeSpaces the AZD tooling is already preinstalled.

To provision and deploy:
```bash
azd up
```

## Source Code

The key code that makes the prompting and completion work is as follows in [ask.cs](app/ask.cs).  The `/api/ask` function and route expects a prompt to come in the POST body.  The templating pattern is used to define the input binding for prompt and the underlying parameters for OpenAI models like the maxTokens and the AI model to use for chat.  

The whois function expects a name to be sent in the route `/api/whois/<name>` and you get to see a different example of a route and parameter coming in via http GET.  

### Simple prompting and completions with gpt

```csharp
/// <summary>
/// This sample takes a prompt as input, sends it directly to the OpenAI completions API, and results the 
/// response as the output.
/// </summary>
[Function("ask")]
public static IActionResult GenericCompletion(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    [TextCompletionInput("{Prompt}", Model = "%CHAT_MODEL_DEPLOYMENT_NAME%")] TextCompletionResponse response,
    ILogger log)
{
    string text = response.Content;
    return new OkObjectResult(text);
}


/// <summary>
/// This sample demonstrates the "templating" pattern, where the function takes a parameter
/// and embeds it into a text prompt, which is then sent to the OpenAI completions API.
/// </summary>
[Function("whois")]
public static IActionResult WhoIs(
    [HttpTrigger(AuthorizationLevel.Function, Route = "whois/{name}")] HttpRequestData req,
    [TextCompletionInput("Who is {name}?", Model = "%CHAT_MODEL_DEPLOYMENT_NAME%")] TextCompletionResponse response)
{
    return new OkObjectResult(response.Content);
}
```

### Stateful ChatBots with gpt

The stateful chatbot is shown in [chat.cs](app/chat.cs) routing to `/api/chats`.  This is a stateful function meaning you can create or ask for a session by <chatId> and continue where you left off with the same context and memories stored by the function binding (backed Table storage).  This makes use of the Assistants feature of the Azure Functions OpenAI extension that has a set of inputs and outputs for this case.  

To create or look up a session we have the CreateChatBot as an http PUT function.  Note how the code will reuse your AzureWebJobStorage connection.  The output binding of `AssistantCreateOutput` will actually kick off the create.  

```csharp
public class CreateChatBotOutput
{
    [AssistantCreateOutput()]
    public AssistantCreateRequest? ChatBotCreateRequest { get; set; }

    [HttpResult]
    public IActionResult? HttpResponse { get; set; }
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
```

Subsequent chat messages are sent to the chat as http POST, being careful to use the same chatId.  This makes use of the `AssistantPostInput` input binding to take message as input and do the completion, while also automatically pulling context and memories for the session, and also saving your new state.
```csharp
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
```

The [test.http](test.http) file is helpful to see how clients and APIs should call these functions, and to learn the typical flow.  

You can customize this or learn more using [Open AI Triggers and Bindings extension](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-openai?tabs=isolated-process&pivots=programming-language-csharp).  
