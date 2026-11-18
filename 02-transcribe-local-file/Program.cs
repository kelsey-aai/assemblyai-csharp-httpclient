using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set the ASSEMBLYAI_API_KEY environment variable first.");
    return 1;
}

var path = args.Length > 0 ? args[0] : "audio.mp3";
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Audio file not found: {path}");
    return 1;
}

using var http = new HttpClient { BaseAddress = new Uri("https://api.assemblyai.com") };
http.DefaultRequestHeaders.Add("Authorization", apiKey);

// Step 1: upload the raw bytes of the local file
await using var fileStream = File.OpenRead(path);
var upload = await http.PostAsync("/v2/upload", new StreamContent(fileStream));
upload.EnsureSuccessStatusCode();
var uploadUrl = (await upload.Content.ReadFromJsonAsync<JsonElement>())
    .GetProperty("upload_url").GetString();

// Step 2: submit the uploaded file for transcription
var submit = await http.PostAsJsonAsync("/v2/transcript", new
{
    audio_url = uploadUrl,
    speech_models = new[] { "assemblyai-stt-3-7", "universal-2" },
    language_detection = true
});
submit.EnsureSuccessStatusCode();
var transcriptId = (await submit.Content.ReadFromJsonAsync<JsonElement>())
    .GetProperty("id").GetString();
Console.WriteLine($"Transcript ID: {transcriptId}");

// Step 3: poll until the transcript is ready
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
