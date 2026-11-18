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

// Submit the transcription job
var submit = await http.PostAsJsonAsync("/v2/transcript", new
{
    audio_url = "https://assembly.ai/wildfires.mp3",
    speech_models = new[] { "assemblyai-stt-3-7", "universal-2" },
    language_detection = true
});
submit.EnsureSuccessStatusCode();
var job = await submit.Content.ReadFromJsonAsync<JsonElement>();
var transcriptId = job.GetProperty("id").GetString();
Console.WriteLine($"Transcript ID: {transcriptId}");

// Poll until the transcript is ready
while (true)
{
    var result = await http.GetFromJsonAsync<JsonElement>($"/v2/transcript/{transcriptId}");
    var status = result.GetProperty("status").GetString();

    if (status == "completed")
    {
        Console.WriteLine(result.GetProperty("text").GetString());
        return 0;
    }
    if (status == "error")
    {
        Console.Error.WriteLine($"Transcription failed: {result.GetProperty("error").GetString()}");
        return 1;
    }

    await Task.Delay(TimeSpan.FromSeconds(3));
}
