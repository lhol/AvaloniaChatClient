# AvaloniaChatClient – Build, Start & Release

**Plattformen:** Windows, macOS, Linux  
**Voraussetzungen:** .NET 10 SDK, Git

---

## Inhaltsverzeichnis

1. [Voraussetzungen](#1-voraussetzungen)
2. [Repository klonen](#2-repository-klonen)
3. [Bauen (Build)](#3-bauen-build)
4. [Starten (Entwicklung)](#4-starten-entwicklung)
5. [Konfiguration](#5-konfiguration)
6. [Veröffentlichen (Release)](#6-veröffentlichen-release)
7. [Schnellstart mit einem lokalen LLM](#7-schnellstart-mit-einem-lokalen-llm)

---

## 1. Voraussetzungen

| Tool | Version | Download |
|---|---|---|
| .NET SDK | 10.0+ | https://dot.net |
| Git | beliebig | https://git-scm.com |
| (optional) Ollama | beliebig | https://ollama.com |

Prüfen:
```powershell
dotnet --version   # 10.0.xxx
```

---

## 2. Repository klonen

```powershell
git clone https://github.com/lhol/AvaloniaChatClient.git
cd AvaloniaChatClient
```

---

## 3. Bauen (Build)

### Alle Projekte auf einmal

```powershell
dotnet build AvaloniaChatClient.sln
```

### Nur Backend

```powershell
dotnet build AvaloniaChatClient.Backend\AvaloniaChatClient.Backend.csproj
```

### Nur Desktop-Frontend

```powershell
dotnet build AvaloniaChatClient.Desktop\AvaloniaChatClient.Desktop.csproj
```

### Nur Adapter-Bibliothek

```powershell
dotnet build AvaloniaChatClient.Adapters\AvaloniaChatClient.Adapters.csproj
```

---

## 4. Starten (Entwicklung)

Backend und Frontend müssen **parallel** laufen. Öffne zwei Terminal-Fenster:

### Terminal 1 – Backend starten

```powershell
dotnet run --project AvaloniaChatClient.Backend\AvaloniaChatClient.Backend.csproj
```

Das Backend lauscht auf `http://localhost:5100`.  
Gesundheitscheck: http://localhost:5100/health

### Terminal 2 – Desktop-Frontend starten

```powershell
dotnet run --project AvaloniaChatClient.Desktop\AvaloniaChatClient.Desktop.csproj
```

### Alternativ: Beide zusammen (PowerShell)

```powershell
# Backend im Hintergrund starten
Start-Process dotnet -ArgumentList "run --project AvaloniaChatClient.Backend\AvaloniaChatClient.Backend.csproj"

# Kurz warten, dann Frontend starten
Start-Sleep 2
dotnet run --project AvaloniaChatClient.Desktop\AvaloniaChatClient.Desktop.csproj
```

---

## 5. Konfiguration

### Backend-Port

Der Backend-Port ist in `AvaloniaChatClient.Backend/appsettings.json` konfigurierbar:

```json
{
  "Urls": "http://localhost:5100"
}
```

Alternativ als Umgebungsvariable:
```powershell
$env:ASPNETCORE_URLS = "http://localhost:5100"
dotnet run --project AvaloniaChatClient.Backend\...
```

### Frontend Backend-URL

Die Frontend-URL zum Backend ist in `AvaloniaChatClient/Services/BackendApiClient.cs` hinterlegt (Standard: `http://localhost:5100`).

### Datenspeicher

Alle Daten (Server-Profile, Sessions, Skills) werden gespeichert in:

| Plattform | Pfad |
|---|---|
| Windows | `%AppData%\AvaloniaChatClient\` |
| Linux/macOS | `~/.local/share/AvaloniaChatClient/` |

---

## 6. Veröffentlichen (Release)

### Windows – Selbstständige Executable (self-contained)

#### Backend

```powershell
dotnet publish AvaloniaChatClient.Backend\AvaloniaChatClient.Backend.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish\backend
```

#### Desktop-Frontend

```powershell
dotnet publish AvaloniaChatClient.Desktop\AvaloniaChatClient.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish\desktop
```

### Linux

```bash
dotnet publish AvaloniaChatClient.Backend/AvaloniaChatClient.Backend.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/backend

dotnet publish AvaloniaChatClient.Desktop/AvaloniaChatClient.Desktop.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/desktop
```

### macOS (Apple Silicon)

```bash
dotnet publish AvaloniaChatClient.Backend/AvaloniaChatClient.Backend.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o publish/backend

dotnet publish AvaloniaChatClient.Desktop/AvaloniaChatClient.Desktop.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o publish/desktop
```

### Ausgabe starten

```powershell
# Windows
.\publish\backend\AvaloniaChatClient.Backend.exe
.\publish\desktop\AvaloniaChatClient.Desktop.exe
```

```bash
# Linux / macOS
./publish/backend/AvaloniaChatClient.Backend &
./publish/desktop/AvaloniaChatClient.Desktop
```

### Release mit GitHub Actions (empfohlen)

Erstelle `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
        include:
          - os: windows-latest
            rid: win-x64
          - os: ubuntu-latest
            rid: linux-x64
          - os: macos-latest
            rid: osx-arm64

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Publish Backend
        run: |
          dotnet publish AvaloniaChatClient.Backend/AvaloniaChatClient.Backend.csproj \
            -c Release -r ${{ matrix.rid }} --self-contained true \
            -p:PublishSingleFile=true -o artifacts/backend

      - name: Publish Desktop
        run: |
          dotnet publish AvaloniaChatClient.Desktop/AvaloniaChatClient.Desktop.csproj \
            -c Release -r ${{ matrix.rid }} --self-contained true \
            -p:PublishSingleFile=true -o artifacts/desktop

      - name: Upload artifacts
        uses: actions/upload-artifact@v4
        with:
          name: release-${{ matrix.rid }}
          path: artifacts/
```

Tag setzen und pushen:
```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## 7. Schnellstart mit einem lokalen LLM

### Ollama installieren und Modell laden

```bash
# Ollama installieren (https://ollama.com)
ollama pull llama3
ollama serve          # läuft auf http://localhost:11434
```

### Anwendung starten

```powershell
# Backend
dotnet run --project AvaloniaChatClient.Backend\AvaloniaChatClient.Backend.csproj

# Frontend (neues Terminal)
dotnet run --project AvaloniaChatClient.Desktop\AvaloniaChatClient.Desktop.csproj
```

### Server-Profil anlegen

Im Tab **„Server"**:
- **Name:** Lokales Ollama
- **URL:** `http://localhost`
- **Port:** `11434`
- **Token:** *(leer)*
- **Protokoll:** `OpenAI`

Klick auf **„Test"** → grünes Häkchen = Verbindung OK.

### Erste Chat-Session

1. Klick auf **„＋ Neue Session"**
2. Server wählen → Modell eingeben: `llama3`
3. **„Erstellen"** → Nachricht tippen → **„Senden"**

---

## Projektstruktur (Überblick)

```
AvaloniaChatClient.sln
├── AvaloniaChatClient/          # Avalonia UI (MVVM)
├── AvaloniaChatClient.Adapters/ # LLM-Adapter (ILlmAdapter, OpenAiAdapter)
├── AvaloniaChatClient.Backend/  # ASP.NET Core Minimal API
├── AvaloniaChatClient.Desktop/  # Desktop-Host
├── AvaloniaChatClient.Android/  # Android-Host
├── AvaloniaChatClient.iOS/      # iOS-Host
├── AvaloniaChatClient.Browser/  # WASM-Host
└── docs/
    ├── ARCHITECTURE.md
    ├── API.md
    ├── Adapter-OpenAI.md
    └── BUILD.md                 # Dieses Dokument
```

---

*Letzte Aktualisierung: 2025-07*
