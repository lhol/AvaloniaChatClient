# Feature-Spezifikation v0.1

**Projekt:** AvaloniaChatClient  
**Version:** 0.1  
**Stand:** 2025-07  
**Status:** Spezifikation – zur Implementierung freigegeben

---

## Übersicht

Dieses Dokument beschreibt acht Verbesserungen und Neufunktionen für das Avalonia-Chat-Frontend. Die Änderungen betreffen die Historien-Verwaltung, Metadaten-Anzeige, Token-Zählung und UX-Details (Tooltips, automatische Sichtbarkeit).

---

## F-01 · Doppeltes Öffnen von Historien verhindern

### Problem
Ein bereits geöffneter Chat kann über die Historien-Liste erneut geöffnet werden, was zu doppelten Session-Tabs führt.

### Anforderung
- Der **„Öffnen"-Button** eines Historien-Eintrags ist deaktiviert (`IsEnabled = false`), wenn die zugehörige Session bereits als aktiver Tab in der Session-Liste vorhanden ist.
- Die Prüfung erfolgt anhand der `SessionId` (GUID).
- Wird ein offener Tab geschlossen, wird der Button automatisch wieder aktiviert.

### Technische Umsetzung
- `HistoryViewModel`: neue `bool`-Property `IsAlreadyOpen` pro `SessionSummary`-Eintrag oder ein `IValueConverter` der gegen `MainViewModel.Sessions` prüft.
- Empfohlen: `ObservableCollection<Guid> OpenSessionIds` in `MainViewModel` → Binding im AXAML via `MultiBinding` oder eigener Converter.
- `HistoryView.axaml`: `IsEnabled="{Binding IsAlreadyOpen, Converter={...}}"` am Öffnen-Button.

---

## F-02 · Historien-Download (JSON & Markdown)

### Anforderung
- Pro Historien-Eintrag gibt es einen **Download-Button** mit Dropdown für zwei Formate:
  - **JSON** – vollständige Rohdaten der Session (wie vom Backend geliefert).
  - **Markdown** – menschenlesbare Chat-Darstellung (siehe Format unten).
- Der Dateiname lautet `<Titel>_<Datum>.json` bzw. `<Titel>_<Datum>.md`.
- Der Download öffnet einen **Datei-Speichern-Dialog** (Avalonia `SaveFileDialog`).

### Markdown-Format

```markdown
# <Session-Titel>

**Erstellt:** 2025-07-01 14:22  
**Modell:** llama3  
**Server:** LocalLM

---

**User** · 14:22:05

Wie funktioniert ein Transformer?

---

**Assistant** · 14:22:08 · TTFT: 312 ms | Gesamt: 4 201 ms | Tokens: 487

Ein Transformer ist...

---
```

### Technische Umsetzung
- Neuer statischer `ChatExporter`-Dienst in `AvaloniaChatClient/Services/ChatExporter.cs`:
  - `ExportToJsonAsync(ChatSession session, string filePath)`
  - `ExportToMarkdownAsync(ChatSession session, string filePath)` mit internem `BuildMarkdown()`
- `HistoryViewModel`: `ExportJsonCommand(SessionSummary)` und `ExportMarkdownCommand(SessionSummary)` – laden zunächst die vollständige Session via `BackendApiClient.GetSessionAsync()`.
- Fehler werden über `AppErrorService` gemeldet.

---

## F-03 · Token- / Wort-Zählung

### Anforderung
- Pro Nachricht werden gezählt und gespeichert:
  - **Input-Token/-Worte** (Nutzernachricht, die die Anfrage ausgelöst hat)
  - **Output-Token/-Worte** (Assistenten-Antwort)
- Sofern das Backend echte Token-Zahlen liefert (OpenAI `usage`-Objekt), werden diese verwendet. Andernfalls wird die **Wortanzahl** (`string.Split(' ').Length`) als Näherungswert gespeichert.
- Die Werte werden zusammen mit TTFT/TotalMs persistiert (Backend-Modell `ChatMessage`).

### Datenmodell-Änderungen
```
ChatMessage:
  + InputTokens  : int?   // Token/Worte der Nutzernachricht
  + OutputTokens : int?   // Token/Worte der Assistenten-Antwort
```

### Technische Umsetzung
- **Backend** (`ChatMessage`-Model, `SessionService`): Felder ergänzen.
- **Adapter** (`ILlmAdapter` / `LlmChunk`): Optional `UsageTokens` aus OpenAI-`usage`-Response auslesen und im abschließenden Done-Chunk mitliefern.
- **Frontend** (`ChatMessageViewModel`): `InputTokens`, `OutputTokens` als Properties; Anzeige abhängig von F-04.
- Näherungswert-Zählung im `ChatSessionViewModel` falls keine echten Tokens vorhanden.

---

## F-04 · Metadaten ein-/ausblenden

### Anforderung
- Ein **Toggle-Button** (Symbol: `ℹ` oder `⏱`) in der Chat-Ansicht blendet die Metadaten-Zeile unter jeder Assistenten-Nachricht ein und aus.
- **Metadaten-Zeile** enthält: `TTFT: X ms | Gesamt: Y ms | Tokens: Z` (sobald F-03 implementiert).
- Der Zustand (ein/aus) wird **pro Session** gehalten (nicht global persistiert).
- Standard: **ausgeblendet** (um den Chat übersichtlich zu halten).

### Technische Umsetzung
- `ChatSessionViewModel`: `bool ShowMetadata` (ObservableProperty, Standard `false`).
- `ChatSessionView.axaml`: Toggle-Button bindet an `ShowMetadata`; Metadaten-TextBlock hat `IsVisible="{Binding $parent[ItemsControl].((vm:ChatSessionViewModel)DataContext).ShowMetadata}"`.
- `ToggleMetadataCommand` in `ChatSessionViewModel`.

---

## F-05 · Neue Chats direkt in der Historie anzeigen

### Anforderung
- Sobald eine neue Session erstellt wird, erscheint sie **sofort** in der Historien-Liste – ohne manuelles Neu-Laden.
- Die Liste bleibt live synchron: Neue Sessions tauchen oben in der Liste auf.

### Technische Umsetzung
- `MainViewModel.CreateSessionAsync()`: Nach erfolgreichem Anlegen der Session `HistoryViewModel.Sessions` um einen neuen `SessionSummary`-Eintrag ergänzen (Insert an Index 0).
- Kein zusätzlicher Backend-Call erforderlich – die Daten sind bereits durch die Erstellungs-Response vorhanden.
- `HistoryViewModel.Sessions` muss als `ObservableCollection<SessionSummary>` vorliegen (bereits der Fall).

---

## F-06 · Geschlossener Chat sofort in der Historie verfügbar

### Anforderung
- Nach dem Schließen eines Chat-Tabs (✕-Button) ist die Session **sofort** über die Historien-Liste wieder öffenbar.
- Die Session ist bereits in der Historien-Liste vorhanden (durch F-05), der entsprechende Eintrag wird durch F-01 nur reaktiviert (Button wird enabled).
- Der Session-Tab wird aus `MainViewModel.Sessions` entfernt; der Historien-Eintrag bleibt.

### Technische Umsetzung
- `MainViewModel.CloseSessionCommand`: Entfernt die Session aus `Sessions` und `OpenSessionIds` (F-01).
- Kein weiterer Aufwand, wenn F-01 und F-05 implementiert sind.

---

## F-07 · Tooltip mit GUID auf Chat-Tabs

### Anforderung
- Beim Hovern über einen geöffneten **Chat-Tab** wird die Session-GUID als Tooltip angezeigt.
- Format: `ID: 3f2a1b4c-...`

### Technische Umsetzung
- `MainView.axaml`: Am Tab-Button `ToolTip.Tip="{Binding SessionId, StringFormat='ID: {0}'}"` ergänzen.
- `ChatSessionViewModel`: `SessionId` (bereits vorhanden als `Guid`) muss öffentlich und bindbar sein.

---

## F-08 · Tooltip mit GUID auf Historien-Einträgen

### Anforderung
- Beim Hovern über einen **Historien-Listeneintrag** wird die Session-GUID als Tooltip angezeigt.
- Format: `ID: 3f2a1b4c-...`

### Technische Umsetzung
- `HistoryView.axaml`: Am Listeneintrag-Panel oder dem übergeordneten Container `ToolTip.Tip="{Binding Id, StringFormat='ID: {0}'}"` ergänzen.
- `SessionSummary.Id` ist bereits vorhanden.

---

## Abhängigkeiten zwischen Features

```
F-05 → F-06  (F-06 setzt voraus, dass die Session durch F-05 schon in der Liste ist)
F-01 → F-06  (F-06 reaktiviert nur den Button, den F-01 deaktiviert)
F-03 → F-04  (Token-Anzeige in Metadaten setzt F-03 voraus; F-04 ist aber unabhängig implementierbar)
```

---

## Betroffene Dateien

| Datei | Features |
|---|---|
| `Backend/Models/ChatSession.cs` | F-03 |
| `Backend/Services/SessionService.cs` | F-03 |
| `Backend/Adapters/OpenAiAdapter.cs` | F-03 (optional, echte Tokens) |
| `Frontend/Models/ChatSession.cs` | F-03 |
| `Frontend/Services/ChatExporter.cs` | F-02 (neu) |
| `Frontend/Services/BackendApiClient.cs` | F-02 |
| `Frontend/ViewModels/MainViewModel.cs` | F-01, F-05, F-06 |
| `Frontend/ViewModels/HistoryViewModel.cs` | F-01, F-02 |
| `Frontend/ViewModels/ChatSessionViewModel.cs` | F-03, F-04 |
| `Frontend/ViewModels/ChatMessageViewModel.cs` | F-03, F-04 |
| `Frontend/Views/MainView.axaml` | F-01, F-05, F-07 |
| `Frontend/Views/HistoryView.axaml` | F-01, F-02, F-08 |
| `Frontend/Views/ChatSessionView.axaml` | F-04 |

---

## Akzeptanzkriterien (Zusammenfassung)

| ID | Kriterium |
|---|---|
| F-01 | Öffnen-Button deaktiviert wenn Session bereits als Tab offen |
| F-02 | JSON und MD-Download mit korrektem Dateinamen und Inhalt |
| F-02 | MD-Format enthält Titel, Datum, Modell, alle Nachrichten mit Zeitstempel und Metadaten |
| F-03 | Token/Wortzählung wird gespeichert und in Metadaten-Zeile angezeigt |
| F-04 | Toggle blendet Metadaten ein/aus; Standard ist ausgeblendet |
| F-05 | Neue Session erscheint sofort in der Historien-Liste |
| F-06 | Geschlossener Chat ist sofort über Historien wieder öffenbar |
| F-07 | Hover über Chat-Tab zeigt `ID: <guid>` |
| F-08 | Hover über Historien-Eintrag zeigt `ID: <guid>` |
