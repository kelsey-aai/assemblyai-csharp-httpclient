using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set the ASSEMBLYAI_API_KEY environment variable first.");
    return 1;
}

using var http = new HttpClient { BaseAddress = new Uri("https://api.assemblyai.com") };
http.DefaultRequestHeaders.Add("Authorization", apiKey);

// Sentiment Analysis runs on AssemblyAI-STT-3.7 for English audio
var submit = await http.PostAsJsonAsync("/v2/transcript", new
{
    audio_url = "https://assembly.ai/wildfires.mp3",
    speech_models = new[] { "assemblyai-stt-3-7" },
    language_code = "en",
    sentiment_analysis = true
});
submit.EnsureSuccessStatusCode();
var transcriptId = (await submit.Content.ReadFromJsonAsync<JsonElement>())
    .GetProperty("id").GetString();

JsonElement result;
while (true)
{
    result = await http.GetFromJsonAsync<JsonElement>($"/v2/transcript/{transcriptId}");
    var status = result.GetProperty("status").GetString();
    if (status == "completed") break;
    if (status == "error")
    {
        Console.Error.WriteLine($"Transcription failed: {result.GetProperty("error").GetString()}");
        return 1;
    }
    await Task.Delay(TimeSpan.FromSeconds(3));
}

// One result per sentence: text, sentiment, confidence and timestamps in milliseconds
foreach (var sentence in result.GetProperty("sentiment_analysis_results").EnumerateArray())
{
    Console.WriteLine(
        $"[{sentence.GetProperty("start").GetInt32()}-{sentence.GetProperty("end").GetInt32()} ms] " +
        $"{sentence.GetProperty("sentiment").GetString()} " +
        $"({sentence.GetProperty("confidence").GetDouble():F2}): " +
        sentence.GetProperty("text").GetString());
}
return 0;
