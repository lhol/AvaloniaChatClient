# AvaloniaChatClient – Sofwarearchitektur

**Stand:** 2025-07  
**Plattformen:** Windows, macOS, Linux, Android, iOS, Browser (WASM)  
**Tech-Stack:** C# 14 / .NET 10, Avalonia UI, CommunityToolkit.Mvvm

---

## Inhaltsverzeichnis

1. [Überblick](#1-überblick)
2. [Schichtenmodell](#2-schichtenmodell)
3. [Architekturentscheidungen](#3-architekturentscheidungen)
4. [Frontend – Funktionsblöcke](#4-frontend--funktionsblöcke)
5. [Backend – REST API](#5-backend--rest-api)
6. [LLM-Protokoll-Adapter](#6-llm-protokoll-adapter)
7. [Datenhaltung](#7-datenhaltung)
8. [Schnittstellen & APIs](#8-schnittstellen--apis)
9. [Aufruffluss zwischen den Schichten](#9-aufruffluss-zwischen-den-schichten)
10. [Projektstruktur](#10-projektstruktur)

---

## 1. Überblick

`AvaloniaChatClient` ist ein plattformübergreifender KI-Chat-Client. Er verbindet sich über konfigurierbare Server-Profile mit verschiedenen LLM-Backends (Cloud oder lokal gehostet via vLLM, LM Studio, Ollama) und zeichnet die Chat-Historien auf.

```
┌─────────────────────────────────────────────────────────┐
│                  Avalonia UI Frontend                   │
│  ┌──────────────┐ ┌───────────────┐ ┌────────────────┐ │
│  │  Preferences │ │  Chat (Multi- │ │ Session-       │ │
│  │  (Server-    │ │  Session,     │ │ Verwaltung &   │ │
│  │  Profile)    │ │  Streaming)   │ │ Historien      │ │
│  └──────┬───────┘ └──────┬────────┘ └───────┬────────┘ │
└─────────┼────────────────┼──────────────────┼──────────┘
          │  HTTP/REST     │                  │
┌─────────▼────────────────▼──────────────────▼──────────┐
│                  Zentrales Backend (ASP.NET Core)       │
│  /servers   /sessions   /messages   /files   /skills   │
└────────────────────────┬────────────────────────────────┘
                         │ abstrakte ILlmAdapter-Aufrufe
        ┌────────────────┼────────────────────┐
        ▼                ▼                    ▼
┌──────────────┐ ┌──────────────┐  ┌──────────────────┐
│ OpenAI-      │ │ Anthropic-   │  │ LM Studio-       │
│ Adapter      │ │ Adapter      │  │ Adapter          │
└──────────────┘ └──────────────┘  └──────────────────┘
        (Protokoll 4: Platzhalter – eigenes Modul)
```

---

## 2. Schichtenmodell

| Schicht | Technologie | Aufgabe |
|---|---|---|
| **UI** | Avalonia UI, MVVM (CommunityToolkit) | Darstellung, Benutzereingaben |
| **Backend** | ASP.NET Core (Minimal API oder Controller) | Geschäftslogik, Persistenz, Orchestrierung |
| **Adapter** | C# Klassen-Bibliotheken | Protokollübersetzung zu LLM-Servern |
| **Persistenz** | JSON-Dateien, MD-Dateien | Speicherung von Profilen, Historien, Skills |

---

## 3. Architekturentscheidungen

### ADR-001: Separates Backend-Prozess statt In-Process
**Entscheidung:** Das Backend läuft als eigener ASP.NET Core-Prozess.  
**Begründung:** Plattformübergreifende Nutzbarkeit; das Frontend (WASM/Mobile) kann kein In-Process-Backend hosten. Das Frontend kommuniziert per HTTP/REST mit `localhost` (Desktop) oder einem konfigurierbaren Endpunkt (Mobile/Web).

### ADR-002: REST API mit Server-Sent Events (SSE) für Streaming
**Entscheidung:** Chat-Antworten werden per SSE gestreamt (`text/event-stream`).  
**Begründung:** SSE ist auf allen Zielplattformen (inkl. Browser) über `HttpClient` oder `EventSource` verfügbar. Fallback auf einmaligen HTTP-Response, wenn der LLM-Server kein Streaming unterstützt.

### ADR-003: Protokoll-Adapter als eigene Module (ILlmAdapter)
**Entscheidung:** Jedes LLM-Protokoll erhält ein eigenes Modul, das `ILlmAdapter` implementiert.  
**Begründung:** Klare Trennung, einfache Erweiterbarkeit (Platzhalter Protokoll 4), isoliertes Testen.

### ADR-004: JSON für strukturierte Daten, MD für lesbare Zusammenfassungen
**Entscheidung:** Server-Profile und Session-Metadaten als JSON; Chatverläufe und Skill-Zusammenfassungen als Markdown.  
**Begründung:** JSON ist maschinenlesbar und einfach zu (de-)serialisieren; MD ist für Menschen lesbar und kann direkt als Kontext-„Skill" in neue Sessions eingefügt werden.

### ADR-005: MVVM mit CommunityToolkit.Mvvm
**Entscheidung:** Frontend nutzt MVVM-Pattern mit CommunityToolkit.Mvvm.  
**Begründung:** Bereits im Projekt vorhanden; ermöglicht Command-Binding, ObservableProperty und RelayCommand ohne Boilerplate.

### ADR-006: Multi-Session via separate ViewModel-Instanzen
**Entscheidung:** Jede Chat-Session bekommt ein eigenes `ChatSessionViewModel` und ein eigenes Fenster/Tab.  
**Begründung:** Parallele, voneinander unabhängige Unterhaltungen; eigene Historien und Kontexte.

---

## 4. Frontend – Funktionsblöcke

### 4.1 Preferences (Server-Profile)

**ViewModel:** `ServerProfilesViewModel`  
**View:** `ServerProfilesView`

Felder je Profil:
- `Name` (string)
- `Url` (string)
- `Port` (int)
- `Token` (string?, optional)
- `Protocol` (enum: `OpenAI | Anthropic | LmStudio | Custom`)

Aktionen: Hinzufügen, Bearbeiten, Löschen, Verbindungstest.

### 4.2 Chat (Multi-Session)

**ViewModel:** `ChatSessionViewModel` (eine Instanz pro Session)  
**View:** `ChatSessionView` (eigenes Fenster oder Tab)

Funktionen:
- Server-Profil wählen
- Texteingabe, Datei-Anhänge (sofern API es erlaubt)
- Streaming-Anzeige (SSE → UI-Updates per `IAsyncEnumerable<string>`)
- Anzeige: Zeit bis erstes Token (TTFT), Gesamtantwortzeit
- Kontextfenster: Alle bisherigen Nachrichten der Session werden mitgesendet

**SessionManager:** `SessionManagerViewModel`  
Verwaltet die Menge offener `ChatSessionViewModel`-Instanzen.

### 4.3 Session- und Historien-Verwaltung

**ViewModel:** `HistoryViewModel`  
**View:** `HistoryView`

Funktionen:
- Liste aller gespeicherten Sessions
- Session öffnen (als neues Fenster mit bestehender Historie)
- Session als MD-Zusammenfassung exportieren (→ Skill)
- Skill einer neuen Session als Kontext hinzufügen
- Session löschen

---

## 5. Backend – REST API

**Technologie:** ASP.NET Core Minimal API  
**Base-URL (lokal):** `http://localhost:5100`

### Endpunkte

#### Server-Profile

| Methode | Pfad | Beschreibung |
|---|---|---|
| `GET` | `/servers` | Alle Profile laden |
| `POST` | `/servers` | Neues Profil erstellen |
| `PUT` | `/servers/{id}` | Profil aktualisieren |
| `DELETE` | `/servers/{id}` | Profil löschen |
| `POST` | `/servers/{id}/test` | Verbindungstest |

#### Sessions

| Methode | Pfad | Beschreibung |
|---|---|---|
| `GET` | `/sessions` | Alle Sessions (Metadaten) |
| `POST` | `/sessions` | Neue Session anlegen |
| `GET` | `/sessions/{id}` | Session laden (inkl. Nachrichten) |
| `DELETE` | `/sessions/{id}` | Session löschen |
| `POST` | `/sessions/{id}/export` | Zusammenfassung als MD exportieren |

#### Nachrichten / Chat

| Methode | Pfad | Beschreibung |
|---|---|---|
| `POST` | `/sessions/{id}/messages` | Nachricht senden (SSE-Stream) |

Request-Body:
```json
{
  "role": "user",
  "content": "Erkläre Dependency Injection.",
  "files": []
}
```

Response: `text/event-stream` (SSE)
```
data: {"delta": "Dependency", "ttft_ms": 142}
data: {"delta": " Injection ist...", "ttft_ms": null}
data: [DONE]
```

Letztes Event enthält Metadaten:
```
data: {"done": true, "total_ms": 3840, "ttft_ms": 142}
```

#### Dateien

| Methode | Pfad | Beschreibung |
|---|---|---|
| `POST` | `/sessions/{id}/files` | Datei in Session-Kontext hochladen |
| `DELETE` | `/sessions/{id}/files/{fileId}` | Datei aus Kontext entfernen |

#### Skills

| Methode | Pfad | Beschreibung |
|---|---|---|
| `GET` | `/skills` | Alle Skills (MD-Zusammenfassungen) |
| `GET` | `/skills/{id}` | Skill-Inhalt laden |
| `DELETE` | `/skills/{id}` | Skill löschen |

---

## 6. LLM-Protokoll-Adapter

### Interface

```csharp
public interface ILlmAdapter
{
    string ProtocolName { get; }
    bool SupportsFileAttachments { get; }

    IAsyncEnumerable<LlmChunk> StreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
}
```

### Gemeinsame Modelle

```csharp
public record LlmMessage(string Role, string Content);

public record LlmRequest(
    string ModelId,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<FileAttachment>? Files = null,
    double? Temperature = null,
    int? MaxTokens = null);

public record LlmChunk(string Delta, bool IsDone, long? TtftMs = null, long? TotalMs = null);

public record LlmResponse(string Content, long TtftMs, long TotalMs);

public record FileAttachment(string FileName, string MimeType, byte[] Data);
```

### Adapter-Module

| Modul | Protokoll | LLM-Server | Streaming |
|---|---|---|---|
| `OpenAiAdapter` | OpenAI Chat Completions API | vLLM, Ollama (OpenAI-kompatibel) | SSE (`stream: true`) |
| `AnthropicAdapter` | Anthropic Messages API | Claude (Cloud) | SSE (`stream: true`) |
| `LmStudioAdapter` | LM Studio REST API | LM Studio (lokal) | SSE (falls verfügbar), sonst Fallback |
| `CustomAdapter` | Platzhalter | – | konfigurierbar |

#### Streaming-Fallback-Logik

```
1. Adapter sendet Request mit Streaming-Flag.
2. Wenn Server antwortet mit Content-Type: text/event-stream → SSE-Parsing.
3. Wenn Server antwortet mit Content-Type: application/json → 
   Antwort wird einmalig geliefert; Backend wraps als einzelnes LlmChunk mit IsDone=true.
4. Frontend-SSE-Anzeige funktioniert in beiden Fällen identisch.
```

---

## 7. Datenhaltung

Alle Daten liegen im Verzeichnis `%AppData%/AvaloniaChatClient/` (Desktop) bzw. app-lokalem Storage (Mobile).

```
data/
├── servers.json          # Server-Profile
├── sessions/
│   ├── {sessionId}.json  # Session-Metadaten + Nachrichtenhistorie
│   └── ...
├── skills/
│   ├── {skillId}.md      # Skill-Zusammenfassungen (Markdown)
│   └── ...
└── files/
    └── {sessionId}/      # Hochgeladene Dateien je Session
```

### servers.json (Schema)

```json
[
  {
    "id": "uuid",
    "name": "Lokales Ollama",
    "url": "http://localhost",
    "port": 11434,
    "token": null,
    "protocol": "OpenAI"
  }
]
```

### sessions/{id}.json (Schema)

```json
{
  "id": "uuid",
  "title": "Erster Chat",
  "serverId": "uuid",
  "modelId": "llama3",
  "createdAt": "2025-07-01T10:00:00Z",
  "updatedAt": "2025-07-01T10:05:00Z",
  "skillIds": [],
  "messages": [
    { "role": "user", "content": "Hallo!", "timestamp": "..." },
    { "role": "assistant", "content": "Hallo! ...", "timestamp": "...", "ttftMs": 130, "totalMs": 2100 }
  ]
}
```

---

## 8. Schnittstellen & APIs

### Frontend ↔ Backend

- **Protokoll:** HTTP/1.1, REST + SSE
- **Serialisierung:** JSON (`System.Text.Json`)
- **Basis-URL:** konfigurierbar (default `http://localhost:5100`)
- **Auth:** keine (lokal); optional Bearer-Token für Remote-Backend

### Backend ↔ LLM-Adapter

- **Schnittstelle:** `ILlmAdapter` (C# Interface, In-Process)
- **Adapter-Auflösung:** Dependency Injection anhand `ServerProfile.Protocol`
- **Streaming:** `IAsyncEnumerable<LlmChunk>`

### Adapter ↔ LLM-Server

| Adapter | API-Endpunkt | Auth |
|---|---|---|
| OpenAiAdapter | `POST /v1/chat/completions` | Bearer Token |
| AnthropicAdapter | `POST /v1/messages` | `x-api-key` Header |
| LmStudioAdapter | `POST /v1/chat/completions` | optional Bearer |
| CustomAdapter | konfigurierbar | konfigurierbar |

---

## 9. Aufruffluss zwischen den Schichten

### Nachricht senden (Streaming)

```
Benutzer tippt Nachricht & drückt Senden
    │
    ▼
ChatSessionViewModel.SendMessageCommand()
    │ HTTP POST /sessions/{id}/messages (JSON-Body)
    ▼
Backend: MessageEndpoint
    │ Lädt Session + Profil aus JSON
    │ Wählt ILlmAdapter via DI (Protocol-Enum)
    │ Fügt Skills als System-Nachrichten ein
    ▼
ILlmAdapter.StreamAsync(LlmRequest)
    │ HTTP POST an LLM-Server (OpenAI/Anthropic/LmStudio)
    │ Liest SSE-Stream (oder JSON-Fallback)
    ▼
Backend: streamt SSE-Events zurück an Frontend
    │  data: {"delta": "...", "ttft_ms": 142}
    ▼
ChatSessionViewModel: empfängt IAsyncEnumerable<string>
    │ Appends Delta zu AssistantMessage
    │ Aktualisiert TTFT- und Total-Timer in UI
    ▼
Backend (nach [DONE]):
    │ Schreibt komplette Assistenten-Nachricht in sessions/{id}.json
    │ Aktualisiert updatedAt
```

### Session-Export als Skill

```
Benutzer klickt "Als Skill exportieren" in HistoryView
    │
    ▼
HistoryViewModel.ExportSkillCommand(sessionId)
    │ HTTP POST /sessions/{id}/export
    ▼
Backend: SessionEndpoint.Export()
    │ Liest sessions/{id}.json
    │ Erstellt Markdown-Zusammenfassung
    │ Schreibt skills/{skillId}.md
    │ Gibt SkillId zurück
    ▼
HistoryViewModel: zeigt SkillId/Titel in Skill-Liste
```

### Neue Session mit Skill

```
Benutzer legt neue Session an, wählt Skill(s)
    │
    ▼
ChatSessionViewModel: POST /sessions  (body: { skillIds: ["..."] })
    ▼
Backend: legt sessions/{newId}.json an mit skillIds
    │
    │ Beim ersten Message-Request:
    ▼
MessageEndpoint: liest skills/{skillId}.md,
    injiziert MD-Inhalt als system-Nachricht vor Nutzer-Nachrichten
    ▼
ILlmAdapter.StreamAsync(LlmRequest mit System-Nachricht)
```

### Server-Profil Verbindungstest

```
Benutzer klickt "Test" in ServerProfilesView
    │
    ▼
ServerProfilesViewModel.TestConnectionCommand()
    │ HTTP POST /servers/{id}/test
    ▼
Backend: wählt ILlmAdapter, sendet Minimal-Request (z. B. GET /models oder leerer Chat)
    │ Gibt { success: true/false, latencyMs: 42, error: null } zurück
    ▼
ServerProfilesViewModel: zeigt Status-Icon + Latenz
```

---

## 10. Projektstruktur

```
AvaloniaChatClient.sln
│
├── AvaloniaChatClient/              # Gemeinsames UI-Projekt (Avalonia)
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   ├── ChatSessionView.axaml
│   │   ├── ServerProfilesView.axaml
│   │   └── HistoryView.axaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── ChatSessionViewModel.cs
│   │   ├── SessionManagerViewModel.cs
│   │   ├── ServerProfilesViewModel.cs
│   │   └── HistoryViewModel.cs
│   ├── Services/
│   │   └── BackendApiClient.cs      # HTTP-Client gegen Backend REST API
│   └── Models/
│       ├── ServerProfile.cs
│       ├── ChatSession.cs
│       └── ChatMessage.cs
│
├── AvaloniaChatClient.Backend/      # ASP.NET Core Backend (neues Projekt)
│   ├── Endpoints/
│   │   ├── ServerEndpoints.cs
│   │   ├── SessionEndpoints.cs
│   │   ├── MessageEndpoints.cs
│   │   └── SkillEndpoints.cs
│   ├── Services/
│   │   ├── ServerProfileService.cs
│   │   ├── SessionService.cs
│   │   └── SkillService.cs
│   └── Program.cs
│
├── AvaloniaChatClient.Adapters/     # LLM-Adapter (neues Projekt / Bibliothek)
│   ├── ILlmAdapter.cs
│   ├── Models/
│   │   ├── LlmRequest.cs
│   │   ├── LlmChunk.cs
│   │   └── LlmResponse.cs
│   ├── OpenAi/
│   │   └── OpenAiAdapter.cs
│   ├── Anthropic/
│   │   └── AnthropicAdapter.cs
│   ├── LmStudio/
│   │   └── LmStudioAdapter.cs
│   └── Custom/
│       └── CustomAdapter.cs
│
├── AvaloniaChatClient.Desktop/      # Desktop-Host
├── AvaloniaChatClient.Android/      # Android-Host
├── AvaloniaChatClient.iOS/          # iOS-Host
├── AvaloniaChatClient.Browser/      # WASM-Host
│
└── docs/
    └── ARCHITECTURE.md              # Dieses Dokument
```

---

*Dokument wird bei Architekturentscheidungen und API-Änderungen aktualisiert.*
