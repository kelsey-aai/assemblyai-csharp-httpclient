using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set the ASSEMBLYAI_API_KEY environment variable first.");
    return 1;
}

// A 16-bit mono PCM WAV file. Convert any audio with:
// ffmpeg -i input.mp3 -ac 1 -ar 16000 -c:a pcm_s16le audio.wav
var path = args.Length > 0 ? args[0] : "audio.wav";
if (!File.Exists(path))
{
    Console.Error.WriteLine($"WAV file not found: {path}");
    return 1;
}
var (sampleRate, pcm) = ReadPcm16MonoWav(path);

var url = new Uri(
    "wss://streaming.assemblyai.com/v3/ws" +
    "?speech_model=assemblyai-stt-3-7-realtime" +
    $"&sample_rate={sampleRate}" +
    "&encoding=pcm_s16le");

using var ws = new ClientWebSocket();
ws.Options.SetRequestHeader("Authorization", apiKey);
await ws.ConnectAsync(url, CancellationToken.None);

// Read server messages (Begin, Turn, Termination) in the background
var receiving = Task.Run(async () =>
{
    var buffer = new byte[16 * 1024];
    while (ws.State == WebSocketState.Open)
    {
        using var message = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (frame.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine($"Closed: {ws.CloseStatus} {ws.CloseStatusDescription}");
                return;
            }
            message.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);

        using var doc = JsonDocument.Parse(message.ToArray());
        var root = doc.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "Begin":
                Console.WriteLine($"Session started: {root.GetProperty("id").GetString()}");
                break;
            case "Turn":
                // Partial updates arrive while someone speaks; end_of_turn marks the final text
                if (root.GetProperty("end_of_turn").GetBoolean())
                    Console.WriteLine(root.GetProperty("transcript").GetString());
                break;
            case "Termination":
                Console.WriteLine(
                    $"Session ended: {root.GetProperty("audio_duration_seconds")}s of audio");
                return;
        }
    }
});

// Send 50 ms chunks, paced at real time like a live microphone
var chunkBytes = sampleRate * 2 / 20;
for (var offset = 0; offset < pcm.Length && ws.State == WebSocketState.Open; offset += chunkBytes)
{
    var count = Math.Min(chunkBytes, pcm.Length - offset);
    await ws.SendAsync(pcm.AsMemory(offset, count), WebSocketMessageType.Binary, true, CancellationToken.None);
    await Task.Delay(50);
}

// Terminate finalizes the open turn; streaming is billed while the socket stays open
if (ws.State == WebSocketState.Open)
{
    var terminate = Encoding.UTF8.GetBytes("{\"type\":\"Terminate\"}");
    await ws.SendAsync(terminate, WebSocketMessageType.Text, true, CancellationToken.None);
}

await receiving;
if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
return 0;

static (int SampleRate, byte[] Pcm) ReadPcm16MonoWav(string path)
{
    using var reader = new BinaryReader(File.OpenRead(path));
    if (new string(reader.ReadChars(4)) != "RIFF")
        throw new InvalidDataException("Not a RIFF/WAV file.");
    reader.ReadInt32();
    if (new string(reader.ReadChars(4)) != "WAVE")
        throw new InvalidDataException("Not a WAVE file.");

    var sampleRate = 0;
    while (reader.BaseStream.Position < reader.BaseStream.Length)
    {
        var chunkId = new string(reader.ReadChars(4));
        var chunkSize = reader.ReadInt32();
        if (chunkId == "fmt ")
        {
            var format = reader.ReadInt16();
            var channels = reader.ReadInt16();
            sampleRate = reader.ReadInt32();
            reader.ReadBytes(6); // byte rate + block align
            var bitsPerSample = reader.ReadInt16();
            reader.ReadBytes(chunkSize - 16);
            if (format != 1 || channels != 1 || bitsPerSample != 16)
                throw new InvalidDataException("Expected 16-bit mono PCM audio.");
        }
        else if (chunkId == "data")
        {
            return (sampleRate, reader.ReadBytes(chunkSize));
        }
        else
        {
            reader.ReadBytes(chunkSize + (chunkSize % 2)); // skip LIST and other chunks
        }
    }
    throw new InvalidDataException("No data chunk found.");
}
