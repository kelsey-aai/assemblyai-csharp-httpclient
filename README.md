# AssemblyAI from C# with HttpClient

Companion code for [How to use AssemblyAI with C#](https://www.assemblyai.com/blog/how-to-use-assemblyai-with-csharp).

Five small .NET 8 console apps that call AssemblyAI with only the .NET base class library
(`System.Net.Http`, `System.Net.WebSockets`, `System.Text.Json`). No NuGet packages.

| Folder | What it does | Model |
|---|---|---|
| `01-transcribe-url` | Transcribe a hosted audio file, poll until done | `assemblyai-stt-3-7` (+ `universal-2` fallback) |
| `02-transcribe-local-file` | Upload a local file to `/v2/upload`, then transcribe it | `assemblyai-stt-3-7` (+ `universal-2` fallback) |
| `03-streaming-websocket` | Stream a 16-bit mono WAV over the v3 WebSocket in real time | `assemblyai-stt-3-7-realtime` |
| `04-llm-gateway` | Summarize a transcript by ID with LLM Gateway | `claude-sonnet-5-5` |
| `05-sentiment-analysis` | Transcribe with Sentiment Analysis and print per-sentence results | `assemblyai-stt-3-7` (English) |

## 1. Clone

```bash
git clone https://github.com/kelsey-aai/assemblyai-csharp-httpclient
cd assemblyai-csharp-httpclient
```

## 2. Install

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) or later, then build everything:

```bash
dotnet build 01-transcribe-url
dotnet build 02-transcribe-local-file
dotnet build 03-streaming-websocket
dotnet build 04-llm-gateway
dotnet build 05-sentiment-analysis
```

## 3. Configure

Get an API key from your [AssemblyAI dashboard](https://www.assemblyai.com/dashboard/signup) and export it.
Every program reads it from the environment and exits with a message if it is missing.

```bash
export ASSEMBLYAI_API_KEY=<your-key>
```

## 4. Run

```bash
# Transcribe the hosted sample file; prints the transcript ID and text
dotnet run --project 01-transcribe-url

# Transcribe a local file
dotnet run --project 02-transcribe-local-file -- ./audio.mp3

# Stream a WAV file in real time (16-bit mono PCM)
ffmpeg -i input.mp3 -ac 1 -ar 16000 -c:a pcm_s16le audio.wav
dotnet run --project 03-streaming-websocket -- ./audio.wav

# Summarize a transcript with LLM Gateway (use the ID printed by step 01)
dotnet run --project 04-llm-gateway -- <transcript_id>

# Sentiment Analysis on the hosted sample file
dotnet run --project 05-sentiment-analysis
```

Notes:

- Streaming is billed while the WebSocket is open. `03-streaming-websocket` always sends
  `{"type": "Terminate"}` after the last chunk.
- For a voice agent, change `speech_model` in `03-streaming-websocket` to
  `assemblyai-stt-3-7-realtime-agent`.
- Docs: [pre-recorded](https://www.assemblyai.com/docs/getting-started/transcribe-an-audio-file),
  [streaming](https://www.assemblyai.com/docs/api-reference/streaming-api/streaming-api),
  [LLM Gateway](https://www.assemblyai.com/docs/llm-gateway/quickstart),
  [Sentiment Analysis](https://www.assemblyai.com/docs/speech-understanding/sentiment-analysis).
