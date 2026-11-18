using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set the ASSEMBLYAI_API_KEY environment variable first.");
    return 1;
}
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run -- <transcript_id>");
    return 1;
}

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("Authorization", apiKey);

// LLM Gateway replaces {{ transcript }} with the text of the transcript you pass by ID
var response = await http.PostAsJsonAsync(
    "https://llm-gateway.assemblyai.com/v1/chat/completions",
    new
    {
        model = "claude-sonnet-5-5",
        messages = new[]
        {
            new
            {
                role = "user",
                content = "Summarize this transcript in three bullet points.\n\n{{ transcript }}"
            }
        },
        transcript_id = args[0],
        max_tokens = 1000
    });

if (!response.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"LLM Gateway error {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    return 1;
}

var result = await response.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(result
    .GetProperty("choices")[0]
    .GetProperty("message")
    .GetProperty("content")
    .GetString());
return 0;
