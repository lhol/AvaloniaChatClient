# AvaloniaChatClient Backend – API Dokumentation

**Version:** 1.0  
**Base URL:** `http://localhost:5100`  
**Content-Type:** `application/json` (außer SSE-Endpunkte)

---

## Inhaltsverzeichnis

1. [Health](#1-health)
2. [Server-Profile](#2-server-profile)
3. [Sessions](#3-sessions)
4. [Nachrichten / Chat (SSE)](#4-nachrichten--chat-sse)
5. [Skills](#5-skills)
6. [Datenmodelle](#6-datenmodelle)
7. [Fehlerbehandlung](#7-fehlerbehandlung)
8. [SSE-Protokoll Detail](#8-sse-protokoll-detail)

---

## 1. Health

### `GET /health`

Prüft ob der Backend-Prozess läuft.

**Response `200 OK`**
```json
{
  "status": "ok",
  "timestamp": "2025-07-10T10:00:00+00:00"
}
```

---

## 2. Server-Profile

Server-Profile definieren die Verbindungsparameter zu einem LLM-Server.

### `GET /servers`

Gibt alle gespeicherten Server-Profile zurück.

**Response `200 OK`** → `ServerProfile[]`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Lokales Ollama",
    "url": "http://localhost",
    "port": 11434,
    "token": null,
    "protocol": "OpenAI"
  }
]
```

---

### `GET /servers/{id}`

Gibt ein einzelnes Profil zurück.

**Parameter:** `id` (GUID, Pfad)

**Response `200 OK`** → `ServerProfile`  
**Response `404 Not Found`**

---

### `POST /servers`

Erstellt ein neues Server-Profil.

**Request Body** → `CreateServerProfileRequest`
```json
{
  "name": "Lokales Ollama",
  "url": "http://localhost",
  "port": 11434,
  "token": null,
  "protocol": "OpenAI"
}
```

`protocol` Werte: `"OpenAI"` | `"Anthropic"` | `"LmStudio"` | `"Custom"`

**Response `201 Created`** → `ServerProfile` mit generierter `id`  
**Location Header:** `/servers/{id}`

---

### `PUT /servers/{id}`

Aktualisiert ein bestehendes Profil vollständig.

**Parameter:** `id` (GUID, Pfad)  
**Request Body** → `UpdateServerProfileRequest` (gleiche Felder wie Create ohne `id`)

**Response `200 OK`** → `ServerProfile`  
**Response `404 Not Found`**

---

### `DELETE /servers/{id}`

Löscht ein Profil.

**Parameter:** `id` (GUID, Pfad)

**Response `204 No Content`**  
**Response `404 Not Found`**

---

### `POST /servers/{id}/test`

Testet die Verbindung zu einem LLM-Server (sendet `GET /v1/models`).

**Parameter:** `id` (GUID, Pfad)

**Response `200 OK`** → `TestConnectionResponse`
```json
{
  "success": true,
  "latencyMs": 42,
  "error": null
}
```

Bei Fehler:
```json
{
  "success": false,
  "latencyMs": 5000,
  "error": "Connection refused"
}
```

---

## 3. Sessions

Sessions speichern den vollständigen Chat-Verlauf mit einem Modell.

### `GET /sessions`

Gibt Metadaten aller Sessions zurück (keine Nachrichten), sortiert nach `updatedAt` absteigend.

**Response `200 OK`** → `SessionSummary[]`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Erster Chat",
    "serverId": "9b2e4a1c-...",
    "modelId": "llama3",
    "createdAt": "2025-07-01T10:00:00+00:00",
    "updatedAt": "2025-07-01T10:05:00+00:00",
    "messageCount": 6
  }
]
```

---

### `GET /sessions/{id}`

Gibt eine Session mit allen Nachrichten zurück.

**Parameter:** `id` (GUID, Pfad)

**Response `200 OK`** → `ChatSession`
```json
{
  "id": "3fa85f64-...",
  "title": "Erster Chat",
  "serverId": "9b2e4a1c-...",
  "modelId": "llama3",
  "createdAt": "2025-07-01T10:00:00+00:00",
  "updatedAt": "2025-07-01T10:05:00+00:00",
  "skillIds": [],
  "messages": [
    {
      "id": "a1b2c3d4-...",
      "role": "user",
      "content": "Hallo!",
      "timestamp": "2025-07-01T10:00:01+00:00",
      "ttftMs": null,
      "totalMs": null
    },
    {
      "id": "e5f6a7b8-...",
      "role": "assistant",
      "content": "Hallo! Wie kann ich helfen?",
      "timestamp": "2025-07-01T10:00:03+00:00",
      "ttftMs": 130,
      "totalMs": 2100
    }
  ]
}
```

**Response `404 Not Found`**

---

### `POST /sessions`

Erstellt eine neue Session.

**Request Body** → `CreateSessionRequest`
```json
{
  "serverId": "9b2e4a1c-...",
  "modelId": "llama3",
  "title": "Meine neue Session",
  "skillIds": []
}
```

`title` ist optional (default: `"Neue Session"`)  
`skillIds` ist optional – verlinkt Skill-Dateien, die als System-Prompt injiziert werden

**Response `201 Created`** → `ChatSession`  
**Location Header:** `/sessions/{id}`

---

### `DELETE /sessions/{id}`

Löscht eine Session dauerhaft.

**Parameter:** `id` (GUID, Pfad)

**Response `204 No Content`**  
**Response `404 Not Found`**

---

### `POST /sessions/{id}/export`

Exportiert den Chatverlauf als Markdown-Skill-Datei, die in neuen Sessions als Kontext genutzt werden kann.

**Parameter:** `id` (GUID, Pfad)

**Response `200 OK`** → `ExportSkillResponse`
```json
{
  "skillId": "7c8d9e0f-...",
  "title": "Erster Chat"
}
```

**Response `404 Not Found`**

---

## 4. Nachrichten / Chat (SSE)

### `POST /sessions/{id}/messages`

Sendet eine Nutzernachricht und empfängt die Antwort als SSE-Stream.

**Parameter:** `id` (GUID, Pfad)

**Request Body** → `SendMessageRequest`
```json
{
  "content": "Erkläre Dependency Injection.",
  "files": []
}
```

`files` optional: Base64-kodierte Dateianhänge (nur wenn Modell/API es unterstützt):
```json
{
  "content": "Was steht in der Datei?",
  "files": [
    {
      "fileName": "dokument.pdf",
      "mimeType": "application/pdf",
      "base64Data": "JVBERi0x..."
    }
  ]
}
```

**Response `200 OK`** – Content-Type: `text/event-stream`

Jedes SSE-Event hat das Format:
```
data: {json}\n\n
```

**Delta-Chunk** (während des Streamings):
```
data: {"delta":"Depend","isDone":false,"ttftMs":142,"totalMs":null}

data: {"delta":"ency Injection ist...","isDone":false,"ttftMs":142,"totalMs":null}
```

**Abschluss-Chunk** (letztes Event mit Zeitmetriken):
```
data: {"delta":null,"isDone":true,"ttftMs":142,"totalMs":3840}

data: [DONE]
```

**Felder:**

| Feld | Typ | Beschreibung |
|---|---|---|
| `delta` | `string?` | Nächstes Token / Textstück. `null` beim Done-Event. |
| `isDone` | `bool` | `true` = Stream beendet |
| `ttftMs` | `long?` | Zeit bis erstes Token in ms. Gesetzt ab erstem Delta. |
| `totalMs` | `long?` | Gesamtzeit in ms. Nur beim Done-Event gesetzt. |

**Response `404 Not Found`** – Session oder Server-Profil nicht gefunden (Status vor SSE-Start)

---

### Frontend-Implementierung (Pseudocode)

```csharp
// In BackendApiClient.cs
public async IAsyncEnumerable<SseChunk> SendMessageAsync(
    Guid sessionId, string content,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    using var request = new HttpRequestMessage(HttpMethod.Post,
        $"/sessions/{sessionId}/messages");
    request.Content = JsonContent.Create(new { content });

    using var response = await _http.SendAsync(request,
        HttpCompletionOption.ResponseHeadersRead, ct);

    await using var stream = await response.Content.ReadAsStreamAsync(ct);
    using var reader = new StreamReader(stream);

    string? line;
    while ((line = await reader.ReadLineAsync(ct)) is not null)
    {
        if (!line.StartsWith("data:")) continue;
        var data = line["data:".Length..].Trim();
        if (data == "[DONE]") break;
        var chunk = JsonSerializer.Deserialize<SseChunk>(data);
        if (chunk is not null) yield return chunk;
    }
}
```

---

## 5. Skills

Skills sind Markdown-Zusammenfassungen früherer Sessions, die als Kontext in neue Sessions geladen werden können.

### `GET /skills`

Gibt alle verfügbaren Skills zurück, sortiert nach Erstellungsdatum absteigend.

**Response `200 OK`** → `SkillSummary[]`
```json
[
  {
    "id": "7c8d9e0f-...",
    "title": "Erster Chat",
    "createdAt": "2025-07-01T10:05:00+00:00"
  }
]
```

---

### `GET /skills/{id}`

Gibt einen Skill mit vollständigem Markdown-Inhalt zurück.

**Parameter:** `id` (GUID, Pfad)

**Response `200 OK`** → `SkillContent`
```json
{
  "id": "7c8d9e0f-...",
  "title": "Erster Chat",
  "markdown": "# Erster Chat\n\n**Erstellt:** 2025-07-01\n...",
  "createdAt": "2025-07-01T10:05:00+00:00"
}
```

**Response `404 Not Found`**

---

### `DELETE /skills/{id}`

Löscht einen Skill dauerhaft.

**Parameter:** `id` (GUID, Pfad)

**Response `204 No Content`**  
**Response `404 Not Found`**

---

## 6. Datenmodelle

### `ServerProfile`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | Eindeutige ID |
| `name` | `string` | Anzeigename |
| `url` | `string` | Basis-URL (ohne Port), z. B. `http://localhost` |
| `port` | `int` | Port |
| `token` | `string?` | API-Token (optional) |
| `protocol` | `enum` | `OpenAI` \| `Anthropic` \| `LmStudio` \| `Custom` |

### `ChatSession`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | Eindeutige ID |
| `title` | `string` | Anzeigename der Session |
| `serverId` | `guid` | Referenz auf ServerProfile |
| `modelId` | `string` | Modellname, z. B. `llama3`, `gpt-4o` |
| `createdAt` | `datetime` | ISO 8601 UTC |
| `updatedAt` | `datetime` | ISO 8601 UTC |
| `skillIds` | `guid[]` | Verlinkte Skills als Kontext |
| `messages` | `ChatMessage[]` | Vollständige Nachrichtenhistorie |

### `ChatMessage`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | Eindeutige ID |
| `role` | `string` | `"user"` \| `"assistant"` \| `"system"` |
| `content` | `string` | Nachrichtentext |
| `timestamp` | `datetime` | ISO 8601 UTC |
| `ttftMs` | `long?` | Zeit bis erstes Token (nur Assistenten-Nachrichten) |
| `totalMs` | `long?` | Gesamte Antwortzeit (nur Assistenten-Nachrichten) |

### `SessionSummary`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | |
| `title` | `string` | |
| `serverId` | `guid` | |
| `modelId` | `string` | |
| `createdAt` | `datetime` | |
| `updatedAt` | `datetime` | |
| `messageCount` | `int` | Anzahl Nachrichten (ohne Laden der vollen Session) |

### `SseChunk`

| Feld | Typ | Beschreibung |
|---|---|---|
| `delta` | `string?` | Token-Text, `null` beim Done-Event |
| `isDone` | `bool` | Abschluss-Signal |
| `ttftMs` | `long?` | Zeit bis erstes Token |
| `totalMs` | `long?` | Gesamtzeit, nur beim Done-Event |

### `SkillSummary`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | |
| `title` | `string` | |
| `createdAt` | `datetime` | |

### `SkillContent`

| Feld | Typ | Beschreibung |
|---|---|---|
| `id` | `guid` | |
| `title` | `string` | |
| `markdown` | `string` | Vollständiger Markdown-Inhalt |
| `createdAt` | `datetime` | |

---

## 7. Fehlerbehandlung

| HTTP-Status | Bedeutung |
|---|---|
| `200 OK` | Erfolg mit Body |
| `201 Created` | Ressource erstellt, `Location`-Header gesetzt |
| `204 No Content` | Erfolg ohne Body (DELETE) |
| `404 Not Found` | Ressource nicht gefunden |
| `500 Internal Server Error` | Unerwarteter Fehler |

Fehler-Body (wo vorhanden):
```json
{
  "error": "Fehlerbeschreibung"
}
```

---

## 8. SSE-Protokoll Detail

Das Backend folgt dem Standard-SSE-Format (W3C):

```
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive
```

**Event-Format:**
```
data: {JSON}\n\n
```

**Abschluss:**
```
data: [DONE]\n\n
```

**Frontend-Hinweise:**
- Im Browser: `EventSource` API oder `fetch` mit `ReadableStream`
- In C#/Avalonia: `HttpClient` mit `HttpCompletionOption.ResponseHeadersRead` + `StreamReader`
- Die Verbindung wird vom Backend nach `[DONE]` geschlossen
- Bei abgebrochenem Request (Nutzer stoppt) wird der Stream serverseitig beendet (`CancellationToken`)

---

*Dokument automatisch generiert – Stand 2025-07*
