using System.Net;
using System.Text;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;

// A Native AOT smoke test: the client serializes requests and deserializes both a regular
// response and a streaming response through the source-generated JSON context.
var handler = new CannedMessagesHandler();
var client = new AnthropicClient(
    new ClientOptions
    {
        ApiKey = "aot-example-key",
        BaseUrl = "http://localhost",
        HttpClient = new HttpClient(handler),
    }
);

var message = await client.Messages.Create(
    new MessageCreateParams
    {
        MaxTokens = 128,
        Messages = [new MessageParam { Role = "user", Content = "Hello" }],
        Model = "claude-sonnet-4-5",
    }
);

var text = message.Content.Select(block => block.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
Console.WriteLine($"Received message {message.ID}: {text}");

var streamedEvents = 0;
await foreach (
    var streamEvent in client.Messages.CreateStreaming(
        new MessageCreateParams
        {
            MaxTokens = 128,
            Messages = [new MessageParam { Role = "user", Content = "Stream" }],
            Model = "claude-sonnet-4-5",
        }
    )
)
{
    streamedEvents++;
    _ = streamEvent.Type;
}

Console.WriteLine($"Received {streamedEvents} streaming events");

return text == "Hello from Native AOT" && streamedEvents > 0 ? 0 : 1;

sealed class CannedMessagesHandler : HttpMessageHandler
{
    const string ResponseJson = """
        {
          "id": "msg_aot_example",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-5",
          "content": [
            { "type": "text", "text": "Hello from Native AOT", "citations": null }
          ],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "container": null,
          "diagnostics": null,
          "stop_details": null,
          "usage": {
            "input_tokens": 10,
            "output_tokens": 5,
            "cache_creation_input_tokens": null,
            "cache_read_input_tokens": null,
            "cache_creation": null,
            "server_tool_use": null,
            "service_tier": null,
            "speed": null,
            "inference_geo": null,
            "output_tokens_details": null
          }
        }
        """;

    const string StreamResponse = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_aot_stream","type":"message","role":"assistant","model":"claude-sonnet-4-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":0}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello from Native AOT"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":5}}

        event: message_stop
        data: {"type":"message_stop"}

        """;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var streaming =
            body.Contains("\"stream\":true", StringComparison.Ordinal)
            || body.Contains("\"stream\": true", StringComparison.Ordinal);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                streaming ? StreamResponse : ResponseJson,
                Encoding.UTF8,
                streaming ? "text/event-stream" : "application/json"
            ),
        };
    }
}
