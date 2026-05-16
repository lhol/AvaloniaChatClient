# Feature-Spezifikation v0.2

**Projekt:** AvaloniaChatClient  
**Version:** 0.2  
**Stand:** 2025-07  
**Status:** Spezifikation – zur Implementierung freigegeben

---

## Übersicht

Dieses Dokument beschreibt Verbesserungen in drei Bereichen:

1. **Chat-Ansicht** – visuelle Trennung, aktive Session, Kommentare, mehrzeilige Eingabe, Tastenkürzel, Server/Modell-Wechsel im Chat, Metadaten-Erweiterung
2. **Server-Profil** – Defaultmodell pro Server mit Live-Modellabfrage
3. **Historien-Export** – Dateiauswahl-Dialog beim Download

---

## G-01 · Session-Tab-Bereich optisch abgesetzt

### Anforderung
- Der horizontale Session-Tab-Streifen (oberer Bereich des Chat-Tabs) ist visuell vom Chatinhalt getrennt.
- Trennung per farblich abweichendem Hintergrund und/oder einer Trennlinie.

### Technische Umsetzung
- `MainView.axaml`: Session-Tab-Streifen in einen `Border` mit `Background` und `BorderThickness="0,0,0,1"` einfassen.
- Hintergrundfarbe: `SystemControlBackgroundChromeMediumBrush` (leicht abweichend vom Chat-Hintergrund).

---

## G-02 · Aktive Session farblich kennzeichnen

### Anforderung
- Der Tab-Button der aktiven Session ist visuell hervorgehoben (z. B. Accent-Farbe als Hintergrund oder Unterstrich).
- Inaktive Tabs bleiben im Standard-Look.

### Technische Umsetzung
- `MainView.axaml`: am Tab-Button-`DataTemplate` eine Style-Klasse `active` über `Classes.active="{Binding IsActive}"` anwenden.
- `ChatSessionViewModel`: `bool IsActive` (ObservableProperty).
- `MainViewModel.ActivateSessionCommand`: setzt `IsActive = true` an der gewählten VM, bei allen anderen `IsActive = false`.
- Style in `App.axaml` oder `ChatSessionView`-Styles: `Button.active { Background: SystemAccentColor; Foreground: White }`.

---

## G-03 · Session-Kommentar

### Anforderung
- Jede Session kann einen optionalen Freitext-Kommentar haben.
- **Anzeige:**
  - Im Chat-Tab: Kommentar erscheint als zweite Zeile unter dem Session-Titel im Tab-Button.
  - In der Historien-Ansicht: Kommentar erscheint als Subtext unter Titel.
  - Tooltip auf dem Chat-Tab: `ID: <guid>\n<Kommentar>` (wenn vorhanden).
  - Tooltip auf dem Historien-Eintrag: `ID: <guid>\n<Kommentar>` (wenn vorhanden).
- **Editierung:** Siehe G-04.

### Datenmodell-Änderungen
```
ChatSession (Backend + Frontend):
  + Comment : string?   // optionaler Freitext-Kommentar
```

### Technische Umsetzung
- **Backend** `ChatSession`-Record: `string? Comment` ergänzen.
- **Backend** `CreateSessionRequest`: `string? Comment` ergänzen (optional, null = kein Kommentar).
- **Backend** neues Endpoint: `PATCH /sessions/{id}/meta` mit `UpdateSessionMetaRequest(string? Title, string? Comment)`.
- **Frontend** `ChatSession`-Record und `ChatSessionViewModel`: `Comment`-Property ergänzen.
- `MainView.axaml` Tab-Button: zweite Zeile mit `TextBlock Text="{Binding Comment}"` (sichtbar wenn nicht leer).
- Tooltip: `StringFormat='ID: {0}\n{1}'` via MultiBinding auf `SessionId` und `Comment`.
- `HistoryView.axaml`: Kommentar-Zeile unter Titel; Tooltip entsprechend.

---

## G-04 · Session-Titel und -Kommentar editieren

### Anforderung
- In der **Historien-Ansicht**: Titel und Kommentar sind direkt in der Liste als `TextBox` editierbar (Inline-Edit mit Bestätigungs-Button oder Fokus-Verlust speichert).
- In der **Chat-Ansicht**: Rechtsklick auf den Tab-Button öffnet ein Kontextmenü mit dem Eintrag „Titel / Kommentar bearbeiten" → kleines Inline-Popup/Flyout über dem Tab mit zwei Feldern.
- **Speicherung:** `PATCH /sessions/{id}/meta` (G-03-Endpoint).

### Technische Umsetzung
- `SessionService` (Backend): neue Methode `UpdateMetaAsync(Guid id, string? title, string? comment)`.
- `BackendApiClient` (Frontend): `UpdateSessionMetaAsync(Guid id, string? title, string? comment)`.
- `ChatSessionViewModel`: `EditTitleCommand`, `SaveMetaCommand`; `IsEditingMeta`-Flag für Flyout-Sichtbarkeit.
- `HistoryViewModel`: `SaveSessionMetaCommand(SessionSummary)`.
- `MainView.axaml`: `ContextMenu` am Tab-Button mit Menüeintrag → Flyout-Panel (sichtbar wenn `IsEditingMeta`).
- `HistoryView.axaml`: Toggle zwischen `TextBlock` und `TextBox` per `IsEditingMeta`-Property am Summary-ViewModel oder via DataTrigger.

---

## G-05 · Mehrzeilige Eingabe umschaltbar

### Anforderung
- Ein kleiner **Toggle-Button** (Symbol: `⊞` oder `↕`) neben dem Eingabefeld schaltet zwischen einzeiliger und mehrzeiliger Eingabe um.
- **Einzeilig:** `TextBox` mit `AcceptsReturn="False"`, feste Höhe ca. 36 px.
- **Mehrzeilig:** `TextBox` mit `AcceptsReturn="True"`, `MaxHeight="240"`, vertikaler Scrollbar.

### Technische Umsetzung
- `ChatSessionViewModel`: `bool MultilineInput` (ObservableProperty, Standard `false`).
- `ToggleMultilineCommand` wechselt den Zustand.
- `ChatSessionView.axaml`: `AcceptsReturn="{Binding MultilineInput}"`, Height-Binding an Converter oder Style-Klasse.

---

## G-06 · Senden per Shift+Enter

### Anforderung
- Wenn das Eingabefeld fokussiert ist, löst **Shift+Enter** das Senden aus (identisch mit Klick auf „Senden").
- Im **mehrzeiligen** Modus (G-05) fügt **Enter allein** einen Zeilenumbruch ein; **Shift+Enter** sendet.
- Im **einzeiligen** Modus sendet auch **Enter** allein.

### Technische Umsetzung
- `ChatSessionView.axaml.cs` (Code-Behind): `KeyDown`-Handler am `InputBox`:
  ```csharp
  if (e.Key == Key.Enter && (MultilineMode ? e.KeyModifiers.HasFlag(KeyModifiers.Shift) : true))
  {
      vm.SendCommand.Execute(null);
      e.Handled = true;
  }
  ```
- Alternativ: Avalonia `KeyBinding` auf dem `TextBox`.

---

## G-07 · Server- und Modell-Wechsel im Chat

### Anforderung
- Über dem Eingabefeld (neue Zeile) erscheinen:
  - **Server-Dropdown**: Auswahl aus allen gespeicherten Server-Profilen.
  - **Modell-Dropdown**: befüllt aus der Modellliste des gewählten Servers (via API, G-08).
  - **Reload-Button** (Symbol: `↻`) rechts neben dem Modell-Dropdown: aktualisiert die Modellliste des aktuellen Servers.
- Beim Wechsel des Servers wird die Modellliste automatisch neu geladen.
- Die gewählten Werte werden direkt in der Session gespeichert: `PATCH /sessions/{id}/meta` (erweiterter Request, s. G-04).

### Datenmodell-Änderungen
```
UpdateSessionMetaRequest:
  + ServerId : Guid?
  + ModelId  : string?
```

### Technische Umsetzung
- `ChatSessionViewModel`: `ObservableCollection<ServerProfile> AvailableServers`, `ServerProfile? SelectedServer`, `ObservableCollection<string> AvailableModels`, `string? SelectedModel`, `bool IsLoadingModels`.
- `LoadModelsCommand` ruft `GET /servers/{id}/models` auf (G-08-Endpoint).
- `OnSelectedServerChanged` → ruft automatisch `LoadModelsCommand` auf.
- `ChatSessionView.axaml`: neue `Grid`-Zeile über dem Eingabefeld mit den beiden Dropdowns und dem Reload-Button.

---

## G-08 · Modellabfrage per API

### Anforderung
- Neuer Backend-Endpoint: `GET /servers/{id}/models` → liefert `string[]` mit allen verfügbaren Modell-IDs.
- Der Endpoint ruft `GET /v1/models` am konfigurierten LLM-Server ab und gibt die Modellnamen zurück.
- Bei Fehler (Server nicht erreichbar) → leere Liste, kein Absturz.

### Technische Umsetzung
- `ServerEndpoints.cs`: neues `MapGet("/{id:guid}/models", ...)`.
- Parst die OpenAI-kompatible `/v1/models`-Antwort (Array von `{ id: string, ... }`).
- `BackendApiClient` (Frontend): `GetModelsAsync(Guid serverId) → Task<List<string>>`.

---

## G-09 · Server- und Modellname in Metadaten

### Anforderung
- Pro Assistenten-Antwort werden **Servername** und **Modellname** in den Metadaten gespeichert und (wenn `ShowMetadata` aktiv) angezeigt.
- Metadaten-Zeile vollständig: `Server: X | Modell: Y | TTFT: Z ms | Gesamt: W ms | In: A | Out: B`

### Datenmodell-Änderungen
```
ChatMessage (Backend + Frontend):
  + ServerName : string?
  + ModelName  : string?

SseChunk (Done-Frame):
  + ServerName : string?
  + ModelName  : string?
```

### Technische Umsetzung
- **Backend** `ChatMessage` + `SseChunk`: Felder ergänzen.
- **Backend** `MessageEndpoints`: Done-Chunk mit `profile.Name` und `session.ModelId` befüllen; `UpdateLastAssistantMessageAsync` entsprechend erweitern.
- **Frontend** `ChatMessage`, `SseChunk`, `ChatMessageViewModel.MetadataText`: Felder ergänzen.

---

## G-10 · Defaultmodell pro Server-Profil

### Anforderung
- In der Server-Profil-Bearbeitungsmaske gibt es ein **„Standardmodell"-Dropdown**.
- Das Dropdown wird über `GET /servers/{id}/models` (G-08) befüllt.
- Neben dem Dropdown ein **Reload-Button** `↻`.
- Ist kein Modell verfügbar (Server nicht erreichbar), wird `"default"` als Fallback gesetzt.
- Das Standardmodell wird beim Anlegen einer neuen Session als Vorauswahl genutzt.

### Datenmodell-Änderungen
```
ServerProfile:
  + DefaultModel : string   // Standard: "default"

CreateServerProfileRequest:
  + DefaultModel : string?

UpdateServerProfileRequest:
  + DefaultModel : string?
```

### Technische Umsetzung
- **Backend** `ServerProfile`, `CreateServerProfileRequest`, `UpdateServerProfileRequest`: `DefaultModel`-Feld ergänzen (default `"default"`).
- **Backend** `ServerProfileService.UpdateAsync`: Feld persistieren.
- **Frontend** `ServerProfile`-Modell: `DefaultModel` ergänzen.
- `ServerProfilesViewModel`: `ObservableCollection<string> EditDefaultModelOptions`, `LoadDefaultModelsCommand`.
- `ServerProfilesView.axaml`: Dropdown für Defaultmodell + Reload-Button in der Bearbeitungsmaske.
- `MainViewModel.OpenNewSessionDialogAsync`: `NewSessionModel` mit `SelectedServer.DefaultModel` vorbelegen.

---

## G-11 · Dateiauswahl beim Historien-Export (bestehend reparieren)

### Anforderung
- Beim Klick auf „⬇ JSON" oder „⬇ MD" öffnet sich der Datei-Speichern-Dialog (Avalonia `StorageProvider.SaveFilePickerAsync`) mit vorausgefülltem Dateinamen.
- Der Export startet erst nach Bestätigung durch den Nutzer.
- **Hinweis:** Dies ist bereits teilweise implementiert (v0.1, F-02). In v0.1 fehlt jedoch die korrekte Übergabe des `TopLevel`-Objekts bei dynamisch geladenen Views. Dieses Feature stellt sicher, dass der Dialog in allen Plattform-Kontexten (Desktop/Mobile) korrekt erscheint.

### Technische Umsetzung
- Sicherstellen, dass `HistoryViewModel.TopLevel` zuverlässig gesetzt wird (bereits in `HistoryView.axaml.cs` via `OnAttachedToVisualTree`).
- Prüfen ob `StorageProvider` im Desktop-Kontext verfügbar ist; ggf. Fallback auf `Environment.GetFolderPath(SpecialFolder.Desktop)` wenn kein Dialog verfügbar.

---

## Abhängigkeiten zwischen Features

```
G-08 → G-07   (Modell-Dropdown im Chat benötigt die Model-API)
G-08 → G-10   (Defaultmodell-Dropdown in Server-Profil benötigt dieselbe API)
G-03 → G-04   (Edit-Funktion erweitert G-03-Datenmodell)
G-03 → G-09   (Tooltip nutzt Kommentar-Feld aus G-03)
G-04 → G-07   (PATCH /sessions/{id}/meta wird auch für Server/Modell-Wechsel genutzt)
G-05 → G-06   (Shift+Enter-Logik hängt vom Multiline-Modus ab)
G-02 → G-01   (Aktiv-Styling ist visuell nur sinnvoll wenn Tab-Bereich abgesetzt ist)
```

---

## Betroffene Dateien

| Datei | Features |
|---|---|
| `Backend/Models/ServerProfile.cs` | G-10 |
| `Backend/Models/ChatSession.cs` | G-03, G-09 |
| `Backend/Services/SessionService.cs` | G-03, G-04, G-09 |
| `Backend/Services/ServerProfileService.cs` | G-10 |
| `Backend/Endpoints/ServerEndpoints.cs` | G-08, G-10 |
| `Backend/Endpoints/MessageEndpoints.cs` | G-09 |
| `Frontend/Models/ServerProfile.cs` | G-10 |
| `Frontend/Models/ChatSession.cs` | G-03, G-09 |
| `Frontend/Services/BackendApiClient.cs` | G-03, G-04, G-07, G-08 |
| `Frontend/ViewModels/ChatSessionViewModel.cs` | G-02, G-03, G-05, G-06, G-07, G-09 |
| `Frontend/ViewModels/MainViewModel.cs` | G-02, G-10 |
| `Frontend/ViewModels/HistoryViewModel.cs` | G-03, G-04 |
| `Frontend/ViewModels/ServerProfilesViewModel.cs` | G-08, G-10 |
| `Frontend/Views/ChatSessionView.axaml` | G-05, G-06, G-07, G-09 |
| `Frontend/Views/ChatSessionView.axaml.cs` | G-06 |
| `Frontend/Views/MainView.axaml` | G-01, G-02, G-03, G-04 |
| `Frontend/Views/HistoryView.axaml` | G-03, G-04, G-11 |
| `Frontend/Views/ServerProfilesView.axaml` | G-08, G-10 |

---

## Akzeptanzkriterien (Zusammenfassung)

| ID | Kriterium |
|---|---|
| G-01 | Tab-Streifen visuell vom Chatbereich getrennt (Trennlinie + Hintergrund) |
| G-02 | Aktiver Tab ist farblich hervorgehoben; Wechsel aktualisiert alle Tabs |
| G-03 | Kommentar wird in Tab, Historien-Liste und Tooltip angezeigt |
| G-04 | Titel und Kommentar sind inline editierbar und werden per PATCH gespeichert |
| G-05 | Toggle-Button wechselt Eingabefeld zwischen ein- und mehrzeilig |
| G-06 | Shift+Enter sendet; Enter allein sendet nur im einzeiligen Modus |
| G-07 | Server und Modell sind im Chat per Dropdown wechselbar; Modelliste lädt automatisch |
| G-08 | `GET /servers/{id}/models` liefert Modellliste; leere Liste bei Fehler |
| G-09 | Servername und Modellname erscheinen in der Metadaten-Zeile jeder Antwort |
| G-10 | Defaultmodell pro Server einstellbar mit Live-Modellabfrage und Fallback "default" |
| G-11 | Datei-Speichern-Dialog öffnet sich zuverlässig beim JSON/MD-Export |
