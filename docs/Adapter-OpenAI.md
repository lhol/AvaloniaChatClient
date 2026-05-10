# OpenAI Adapter – Dokumentation

**Projekt:** `AvaloniaChatClient.Adapters`  
**Namespace:** `AvaloniaChatClient.Adapters.OpenAi`  
**Klasse:** `OpenAiAdapter`  
**Stand:** 2025-07

---

## Überblick

Der `OpenAiAdapter` implementiert das `ILlmAdapter`-Interface und kommuniziert mit LLM-Servern, die die **OpenAI Chat Completions API** sprechen. Dazu gehören:

| Server | URL (Standard) | Authentifizierung |
|---|---|---|
| OpenAI Cloud | `https://api.openai.com` | Bearer Token (API Key) |
| Ollama | `http://localhost:11434` | kein Token |
| vLLM | `http://localhost:8000` | optional Bearer |
| LM Studio | `http://localhost:1234` | optional Bearer |

Der Adapter wird im Backend über die `AdapterFactory` automatisch gewählt, wenn ein Server-Profil das Protokoll `OpenAI` oder `LmStudio` verwendet.

---

## Endpunkt

```
POST {baseUrl}/v1/chat/completions
```

Request-Body (JSON):
```json
{
  "model": "llama3",
  "messages": [
    { "role": "system", "content": "Du bist ein hilfreicher Assistent." },
    { "role": "user",   "content": "Erkläre Dependency Injection." }
  ],
  "stream": true,
  "temperature": null,
  "max_tokens": null
}
```

---

## Streaming (SSE)

Wenn der LLM-Server mit `Content-Type: text/event-stream` antwortet, liest der Adapter die Server-Sent Events und gibt jeden Token als `LlmChunk` zurück:

```
data: {"id":"...","choices":[{"delta":{"content":"Depend"},...}]}
data: {"id":"...","choices":[{"delta":{"content":"ency"},...}]}
data: [DONE]
```

Jeder SSE-Frame mit einem `content`-Delta erzeugt:
```csharp
new LlmChunk(Delta: "Depend", IsDone: false, TtftMs: 142, TotalMs: null)
```

Das abschließende `[DONE]`-Frame erzeugt:
```csharp
new LlmChunk(Delta: null, IsDone: true, TtftMs: 142, TotalMs: 3840)
```

### Fallback (kein SSE)

Antwortet der Server mit `Content-Type: application/json`, wird die Antwort einmalig als einzelner Chunk geliefert:
```csharp
// Aus choices[0].message.content
new LlmChunk(Delta: "vollständige Antwort...", IsDone: false, TtftMs: 0)
new LlmChunk(Delta: null, IsDone: true, TtftMs: 0, TotalMs: 0)
```

---

## API (ILlmAdapter)

```csharp
public interface ILlmAdapter
{
    string ProtocolName { get; }          // "OpenAI"
    bool SupportsFileAttachments { get; } // false

    IAsyncEnumerable<LlmChunk> StreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
}
```

### `LlmRequest`

| Feld | Typ | Beschreibung |
|---|---|---|
| `BaseUrl` | `string` | Basis-URL inkl. Port, z.B. `http://localhost:11434` |
| `ModelId` | `string` | Modellname, z.B. `llama3`, `gpt-4o` |
| `Messages` | `IReadOnlyList<LlmMessage>` | Nachrichten-Kontext (system/user/assistant) |
| `Token` | `string?` | Bearer Token (optional) |
| `Temperature` | `double?` | Sampling-Temperatur (optional) |
| `MaxTokens` | `int?` | Maximale Token-Anzahl (optional) |

### `LlmChunk`

| Feld | Typ | Beschreibung |
|---|---|---|
| `Delta` | `string?` | Nächstes Token. `null` beim Done-Event |
| `IsDone` | `bool` | `true` = Stream abgeschlossen |
| `TtftMs` | `long?` | Zeit bis erstes Token in ms |
| `TotalMs` | `long?` | Gesamtzeit. Nur beim Done-Event gesetzt |

### `LlmResponse` (CompleteAsync)

| Feld | Typ | Beschreibung |
|---|---|---|
| `Content` | `string` | Vollständige Antwort |
| `TtftMs` | `long` | Zeit bis Antwort-Start |
| `TotalMs` | `long` | Gesamte Antwortzeit |

---

## Verwendung im Backend

Der Adapter wird **nicht direkt** instanziiert – das übernimmt die `AdapterFactory`:

```csharp
// In AdapterFactory.cs
return profile.Protocol switch
{
    LlmProtocol.OpenAI    => new OpenAiAdapter(http),
    LlmProtocol.LmStudio  => new OpenAiAdapter(http), // LM Studio = OpenAI-kompatibel
    ...
};
```

Der `MessageEndpoint` konsumiert den Adapter:
```csharp
var adapter = adapterFactory.Create(profile);

await foreach (var chunk in adapter.StreamAsync(llmRequest, ct))
{
    if (chunk.IsDone) break;
    if (chunk.Delta is not null)
        await WriteSseAsync(ctx, chunk, ct);
}
```

---

## Direkte Verwendung (ohne Backend)

```csharp
using var http = new HttpClient();
var adapter = new OpenAiAdapter(http);

var request = new LlmRequest(
    BaseUrl: "http://localhost:11434",
    ModelId: "llama3",
    Messages: [new LlmMessage("user", "Erkläre Dependency Injection.")]);

await foreach (var chunk in adapter.StreamAsync(request))
{
    if (chunk.IsDone) break;
    Console.Write(chunk.Delta);
}
```

---

## Kompatibilität

| Feature | Unterstützt |
|---|---|
| Streaming (SSE) | ✅ |
| Fallback (JSON) | ✅ |
| Bearer Token Auth | ✅ |
| System-Nachrichten | ✅ |
| Datei-Anhänge | ❌ (Erweiterung geplant) |
| Function Calling | ❌ (Erweiterung geplant) |

---

## Erweiterung: weiterer Adapter

Um einen neuen Adapter (z.B. `AnthropicAdapter`) hinzuzufügen:

1. Klasse in `AvaloniaChatClient.Adapters/Anthropic/AnthropicAdapter.cs` erstellen, die `ILlmAdapter` implementiert.
2. In `AdapterFactory.cs` den neuen Case hinzufügen:
   ```csharp
   LlmProtocol.Anthropic => new AnthropicAdapter(http),
   ```
3. Frontend: Protokoll `Anthropic` im Server-Profil wählen – alles andere ist automatisch.
