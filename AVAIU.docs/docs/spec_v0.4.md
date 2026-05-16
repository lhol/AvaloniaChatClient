# Implementierungsspezifikation v0.4

## Überblick

Dieses Dokument beschreibt die technische Umsetzung der sechs Features aus `features_v0.4.md` für den AvaloniaChatClient. Die Basis bildet der Stand des Branches `release0.4`.

---

## Feature 1 – Aktive Chat-Session in der linken Sidebar hervorheben

### Ziel
Die aktuell aktive Session in der Sidebar-Session-Liste soll farblich hervorgehoben sein.

### Ist-Zustand
`ChatSessionViewModel` besitzt bereits die Property `IsActive` (bool). Die Session-Buttons in `MainView.axaml` wenden diese jedoch noch nicht als CSS-Klasse an.

### Änderungen

**`AvalonianAiUserinterface/App.axaml`**
- Stil `Button.active` ergänzen (oder erweitern), der `Background` und `Foreground` für aktive Elemente setzt.
  ```xml
  <Style Selector="Button.active">
	<Setter Property="Background" Value="{DynamicResource SystemAccentColor}"/>
	<Setter Property="Foreground" Value="White"/>
  </Style>
  ```

**`AvalonianAiUserinterface/Views/MainView.axaml`**
- An den Session-Buttons innerhalb der `ItemsControl`-Templates die `Classes`-Bindung hinzufügen:
  ```xml
  <Button Classes.active="{Binding IsActive}" .../>
  ```
  Dies gilt für Session-Buttons in Gruppen und Sub-Gruppen.

### Akzeptanzkriterien
- Beim Öffnen oder Aktivieren einer Session wird der zugehörige Button in der Sidebar akzentfarbig dargestellt.
- Beim Wechsel der aktiven Session wechselt die Hervorhebung sofort.
- Keine andere Session ist gleichzeitig hervorgehoben.

---

## Feature 2 – Faltbare linke Sidebar

### Ziel
Die linke Sidebar kann per Button eingeklappt werden (icon-only Modus, ~52 px Breite). Der Fold-Button ist immer sichtbar.

### Ist-Zustand
Die Sidebar ist fest auf 220 px Breite (`ColumnDefinitions="220,Auto,*"` in `MainView.axaml`). Ein Fold-Mechanismus existiert nicht.

### Änderungen

**`AvalonianAiUserinterface/ViewModels/MainViewModel.cs`**
- Property hinzufügen:
  ```csharp
  [ObservableProperty] private bool _isSidebarCollapsed;
  ```
- Command:
  ```csharp
  [RelayCommand]
  private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;
  ```

**`AvalonianAiUserinterface/Views/MainView.axaml`**
- `ColumnDefinitions` durch dynamische Breite ersetzen (Converter oder `Width`-Binding an `IsSidebarCollapsed`):
  - Ausgeklappt: 220 px
  - Eingeklappt: 52 px
- Fold-Button ganz oben im Sidebar-Panel (immer sichtbar):
  ```xml
  <Button Command="{Binding ToggleSidebarCommand}" Content="◀" ToolTip.Tip="Sidebar einklappen"/>
  <!-- Im eingeklappten Zustand: -->
  <Button Command="{Binding ToggleSidebarCommand}" Content="▶" ToolTip.Tip="Sidebar ausklappen"/>
  ```
- Im eingeklappten Zustand: Text-Labels ausblenden (`IsVisible="{Binding !IsSidebarCollapsed}"`), nur Icon-Buttons zeigen.
- Im ausgeklappten Zustand: vollständige Session-Gruppen-Liste sichtbar.

**`AvalonianAiUserinterface/Converters/`** (optional)
- `BoolToSidebarWidthConverter` oder `GridLength`-Converter, falls keine direkten Bindings reichen.

### Layout-Skizze

```
Ausgeklappt (220 px)         Eingeklappt (52 px)
┌────────────────────┐       ┌────┐
│ ◀  Aktive Sessions │       │ ▶  │
│ ──────────────────│       │ 💬 │
│ > Allgemein        │       │ 🕐 │
│   • Session A  ●  │       │ 🖥 │
│   • Session B     │       │ 🎓 │
│ ──────────────────│       └────┘
│ Historie          │
└────────────────────┘
│ Server             │
└────────────────────┘
│ Skills             │
└────────────────────┘
```

### Akzeptanzkriterien
- Fold-Button ist in beiden Zuständen sichtbar und oben positioniert.
- Im eingeklappten Zustand sind Labels unsichtbar; Icon-Buttons bleiben anklickbar. Sie wechseln jeweils zu den Tabs in den Hauptfenstern. Das aktive Icon wird farbig markiert. Wird in eingeklappten Zustand auf das Chat Icon geklickt und sind mehrere Chats offen wird die Seitenleiste ausgeklappt damit man die Chat sieht.
- Zustand überlebt keinen App-Neustart (In-Memory reicht für v0.4).

---

## Feature 3 – Umbenennung „Sessions" → „Aktive Sessions"

### Ziel
Der Header der Sidebar-Session-Sektion heißt „Aktive Sessions" statt „Sessions".

### Ist-Zustand
`MainView.axaml`, Zeile ~52: `<TextBlock Text="Sessions" .../>`.

### Änderungen
- **`MainView.axaml`**: `Text="Sessions"` → `Text="Aktive Sessions"`.

### Akzeptanzkriterien
- Sidebar-Header zeigt „Aktive Sessions".

---

## Feature 4 – Skills-Button in der Sidebar + eigenständiger Skills-Tab

### Ziel
- Ein „Skills"-Button wird in der linken Sidebar ergänzt.
- Rechts erscheint ein neuer Tab „Skills" mit einer dedizierten `SkillsView`.
- Die Skills-Sektion auf der History-Seite wird entfernt.

### Ist-Zustand
- `HistoryView.axaml` enthält eine Skills-Liste.
- Im rechten `TabControl` in `MainView.axaml` gibt es keinen Skills-Tab.
- `HistoryViewModel` lädt und verwaltet Skills (`_skills`, `LoadAsync`).

### Änderungen

#### 4a – Skills-Button in der Sidebar (`MainView.axaml`)
```xml
<Button Content="🎓 Skills"
		Command="{Binding SwitchToSkillsTabCommand}"
		HorizontalAlignment="Stretch"
		HorizontalContentAlignment="Left"
		Padding="10,6" BorderThickness="0"
		Classes.active="{Binding IsSkillsTabActive}"/>
```
Im eingeklappten Modus: Icon-Button mit `🎓`.

#### 4b – Skills-Tab im rechten TabControl (`MainView.axaml`)
```xml
<TabItem Header="Skills">
  <views:SkillsView DataContext="{Binding History}"/>
</TabItem>
```

#### 4c – Neue `SkillsView` (`Views/SkillsView.axaml` + `.axaml.cs`)
Zeigt:
- Liste aller Skills (linke Spalte oder oben) – gebunden an `History.Skills`
- Detail-Panel für ausgewählten Skill (Titel, Erstellungsdatum, Markdown-Inhalt) – gebunden an `History.SelectedSkillItem` / `History.SelectedSkillContent`
- Button „Löschen" (`DeleteSkillCommand`)

```xml
<Grid ColumnDefinitions="200,*">
  <!-- Skill-Liste -->
  <ListBox Grid.Column="0" ItemsSource="{Binding Skills}"
		   SelectedItem="{Binding SelectedSkillItem}">
	<ListBox.ItemTemplate>
	  <DataTemplate>
		<TextBlock Text="{Binding Title}"/>
	  </DataTemplate>
	</ListBox.ItemTemplate>
  </ListBox>
  <!-- Skill-Detail -->
  <StackPanel Grid.Column="1" IsVisible="{Binding SelectedSkillItem, Converter={x:Static ObjectConverters.IsNotNull}}">
	<TextBlock Text="{Binding SelectedSkillContent.Title}" FontSize="16" FontWeight="Bold"/>
	<TextBlock Text="{Binding SelectedSkillContent.CreatedAt}" FontSize="11"/>
	<ScrollViewer>
	  <TextBlock Text="{Binding SelectedSkillContent.Markdown}" TextWrapping="Wrap"/>
	</ScrollViewer>
	<Button Content="🗑 Löschen" Command="{Binding DeleteSkillCommand}"
			CommandParameter="{Binding SelectedSkillItem}" Classes="danger"/>
  </StackPanel>
</Grid>
```

#### 4d – `HistoryViewModel.cs`
- Properties ergänzen:
  ```csharp
  [ObservableProperty] private SkillSummary? _selectedSkillItem;
  [ObservableProperty] private SkillContent? _selectedSkillContent;
  ```
- `partial void OnSelectedSkillItemChanged(SkillSummary? value)` → lädt `SelectedSkillContent` asynchron via `_api.GetSkillAsync(value.Id)`.

#### 4e – `HistoryView.axaml`
- Skills-`ItemsControl`/Liste vollständig entfernen.

#### 4f – `MainViewModel.cs`
- Property:
  ```csharp
  [ObservableProperty] private bool _isSkillsTabActive;
  ```
- Command:
  ```csharp
  [RelayCommand]
  private void SwitchToSkillsTab()
  {
	SelectedTabIndex = 3; // oder korrekter Index des Skills-Tabs
	IsSkillsTabActive = true;
  }
  ```
  Beim Wechsel zu anderen Tabs: `IsSkillsTabActive = false`.

### Akzeptanzkriterien
- Klick auf „Skills" in der Sidebar öffnet den Skills-Tab rechts.
- Skills-Tab zeigt Liste + Detail mit Markdown-Inhalt.
- History-Seite enthält keine Skills-Sektion mehr.
- Löschen eines Skills funktioniert aus dem Skills-Tab heraus.

---

## Feature 5 – Metadaten pro Chat-Zeile sofort aktualisieren

### Ziel
Metadaten (Server, Modell, TTFT, Gesamtzeit, Token) jeder Assistenten-Nachricht werden angezeigt und **live aktualisiert**, sobald Daten eintreffen (während des Streamings).

### Ist-Zustand
- `ChatMessageViewModel` besitzt bereits `TtftMs`, `TotalMs`, `InputTokens`, `OutputTokens`, `ServerName`, `ModelName`, `MetadataText`, `ShowMetadata`.
- `partial void On...Changed` ruft `OnPropertyChanged(nameof(MetadataText))` auf.
- `ChatSessionView.axaml` zeigt `MetadataText` mit `IsVisible="{Binding ShowMetadata}"`.
- `ShowMetadata` wird von der Parent-Session gesteuert.

### Analyse
Das grundlegende Binding ist vorhanden. Die möglichen Lücken sind:
1. `ShowMetadata` wird nicht korrekt vom Session-Level auf Message-Level propagiert.
2. Metadaten werden erst nach vollständigem Stream-Abschluss gesetzt, nicht während.

### Änderungen

**`ChatSessionViewModel.cs`**
- Sicherstellen, dass `ShowMetadata` auf allen `ChatMessageViewModel`-Instanzen gesetzt wird, wenn die Session-weite Property `ShowMetadata` sich ändert:
  ```csharp
  partial void OnShowMetadataChanged(bool value)
  {
	foreach (var msg in Messages)
	  msg.ShowMetadata = value;
  }
  ```
- Beim Hinzufügen einer neuen Nachricht: `msg.ShowMetadata = ShowMetadata`.

- Im Streaming-Loop (`SendMessageAsync` oder entsprechende Methode): Metadaten **inkrementell** setzen, sobald sie im `LlmChunk` ankommen:
  ```csharp
  // Nach jedem Chunk:
  if (chunk.TtftMs.HasValue) assistantMsg.TtftMs = chunk.TtftMs;
  if (chunk.TotalMs.HasValue) assistantMsg.TotalMs = chunk.TotalMs;
  if (chunk.InputTokens.HasValue) assistantMsg.InputTokens = chunk.InputTokens;
  if (chunk.OutputTokens.HasValue) assistantMsg.OutputTokens = chunk.OutputTokens;
  ```
  `LlmChunk` enthält diese Felder bereits (vgl. `LlmModels.cs`).

**`ChatSessionView.axaml`**
- Prüfen, ob `IsVisible="{Binding ShowMetadata}"` korrekt an `ChatMessageViewModel.ShowMetadata` bindet (nicht an die Session-Property). Ggf. korrigieren.

### Akzeptanzkriterien
- Wenn „Metadaten anzeigen" aktiv ist, erscheint die Metadatenzeile unter jeder Assistenten-Nachricht.
- TTFT-Wert erscheint sobald der erste Token eingetroffen ist; Gesamtzeit und Token-Zahlen erscheinen am Stream-Ende.
- Ein späteres Aktivieren von „Metadaten anzeigen" zeigt Metadaten für alle bereits empfangenen Nachrichten sofort.

---

## Feature 6 – Server-Typ-Dropdown im Server-Tab

### Ziel
Im Server-Bearbeitungsformular wird ein Dropdown „Server-Typ" mit den Optionen `LM Studio`, `vllm`, `other` hinzugefügt. Der Wert dient als Hinweis, welche Metadaten-APIs verfügbar sind.

### Ist-Zustand
- `ServerProfile` hat bereits `LlmProtocol Protocol` (Enum: `OpenAI, Anthropic, LmStudio, Custom`).
- `ServerProfilesViewModel` hat `EditProtocol` und `Protocols[]`.
- `ServerProfilesView.axaml` hat noch kein explizites Server-Typ-Dropdown (nur Protokoll).

### Designentscheidung
`LlmProtocol` wird um `Vllm` erweitert. Ein separates `ServerType`-Enum ist nicht nötig – `LlmProtocol` ist der richtige Ort, da er Protokoll und Servertyp vereint.

### Änderungen

**`AvalonianAiUserinterface/Models/ServerProfile.cs`**
```csharp
public enum LlmProtocol { OpenAI, Anthropic, LmStudio, Vllm, Custom }
```
- `LmStudio` ist bereits vorhanden.
- `Vllm` wird neu ergänzt.
- `Custom` entspricht „other".

**`AvalonianAiUserinterface/Views/ServerProfilesView.axaml`** – im Edit-Overlay:
```xml
<TextBlock Text="Server-Typ:" VerticalAlignment="Center"/>
<ComboBox ItemsSource="{Binding Protocols}"
		  SelectedItem="{Binding EditProtocol}"/>
<!-- Hinweistext je nach Typ -->
<TextBlock IsVisible="{Binding EditProtocol, Converter={...}, ConverterParameter=LmStudio}"
		   Text="LM Studio: Metadaten via /v1/models und lokaler Stats-API verfügbar."
		   FontSize="11" Foreground="Gray"/>
<TextBlock IsVisible="{Binding EditProtocol, Converter={...}, ConverterParameter=Vllm}"
		   Text="vllm: Metadaten via /metrics (Prometheus) verfügbar."
		   FontSize="11" Foreground="Gray"/>
```

**`AvalonianAiUserinterface/Converters/`**
- `EnumEqualityConverter` (falls nicht vorhanden) für `IsVisible`-Bindung.

### Akzeptanzkriterien
- Dropdown zeigt alle `LlmProtocol`-Werte an (OpenAI, Anthropic, LM Studio, vllm, other/Custom).
- Auswahl wird beim Speichern persistiert.
- Kontexthinweis erscheint unterhalb des Dropdowns abhängig vom gewählten Typ.

---

## Feature 7 – LM Studio Metadaten-Adapter

### Ziel
Ein Adapter ruft Modell- und Laufzeit-Metadaten von einem LM Studio Server ab. Diese werden in der Chat-Ansicht als Metadaten pro Nachricht angezeigt.

### Recherche: LM Studio API

LM Studio (ab v0.2.x) stellt eine **OpenAI-kompatible REST-API** bereit:

| Endpunkt | Methode | Zweck |
|---|---|---|
| `GET /v1/models` | GET | Liste geladener Modelle |
| `POST /v1/chat/completions` | POST | Chat-Completion (stream + non-stream) |
| `GET /v1/system_info` | GET | Hardware-Infos (RAM, VRAM, CPU) – ab LM Studio 0.3.x |
| `GET /v1/models/{model}/stats` | GET | Per-Modell-Laufzeit-Statistiken (experimentell) |

Die Chat-Completion-Antwort enthält im `usage`-Objekt `prompt_tokens`, `completion_tokens`. Im Stream-Modus liefert das letzte Chunk-Objekt (`finish_reason: "stop"`) das `usage`-Feld.

**TTFT** ist nicht direkt in der LM Studio API exponiert – wird clientseitig durch Zeitmessung zwischen Request-Start und erstem empfangenen Token ermittelt.

### Architektur

```
AvalonianAiMiddleware.Adapters/
├── ILlmAdapter.cs                    (bereits vorhanden)
├── IMetadataProvider.cs              (NEU)
├── Models/
│   ├── LlmModels.cs                  (bereits vorhanden)
│   └── ServerMetadata.cs             (NEU)
├── OpenAi/
│   └── OpenAiAdapter.cs              (bereits vorhanden)
└── LmStudio/
	├── LmStudioAdapter.cs            (NEU – implementiert ILlmAdapter)
	└── LmStudioMetadataProvider.cs   (NEU – implementiert IMetadataProvider)
```

### Neue Dateien

#### `IMetadataProvider.cs`
```csharp
namespace Avaimi.Adapters;

public interface IMetadataProvider
{
	/// <summary>Gibt alle verfügbaren Modell-IDs des Servers zurück.</summary>
	Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default);

	/// <summary>Gibt System-Informationen zurück (RAM, VRAM, GPU-Name etc.).</summary>
	Task<ServerMetadata> GetServerMetadataAsync(CancellationToken ct = default);
}
```

#### `Models/ServerMetadata.cs`
```csharp
namespace Avaimi.Adapters.Models;

public record ServerMetadata(
	string? GpuName,
	long? VramTotalBytes,
	long? VramUsedBytes,
	long? RamTotalBytes,
	long? RamUsedBytes,
	string? ServerVersion);
```

#### `LmStudio/LmStudioMetadataProvider.cs`
```csharp
namespace Avaimi.Adapters.LmStudio;

public class LmStudioMetadataProvider : IMetadataProvider
{
	private readonly HttpClient _http;
	private readonly string _baseUrl;

	public LmStudioMetadataProvider(string baseUrl, string? token = null)
	{
		_baseUrl = baseUrl.TrimEnd('/');
		_http = new HttpClient();
		if (!string.IsNullOrEmpty(token))
			_http.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", token);
	}

	public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
	{
		// GET /v1/models → { "data": [ { "id": "..." }, ... ] }
		var response = await _http.GetFromJsonAsync<ModelsResponse>(
			$"{_baseUrl}/v1/models", ct);
		return response?.Data.Select(m => m.Id).ToList() ?? [];
	}

	public async Task<ServerMetadata> GetServerMetadataAsync(CancellationToken ct = default)
	{
		// GET /v1/system_info (LM Studio 0.3.x+, optional)
		try
		{
			var info = await _http.GetFromJsonAsync<SystemInfoResponse>(
				$"{_baseUrl}/v1/system_info", ct);
			return new ServerMetadata(
				info?.GpuName, info?.VramTotal, info?.VramUsed,
				info?.RamTotal, info?.RamUsed, info?.Version);
		}
		catch
		{
			return new ServerMetadata(null, null, null, null, null, null);
		}
	}

	// Interne DTOs für JSON-Deserialisierung
	private record ModelsResponse(List<ModelEntry> Data);
	private record ModelEntry(string Id);
	private record SystemInfoResponse(
		string? GpuName, long? VramTotal, long? VramUsed,
		long? RamTotal, long? RamUsed, string? Version);
}
```

#### `LmStudio/LmStudioAdapter.cs`
Implementiert `ILlmAdapter` – leitet an `OpenAiAdapter` weiter, da LM Studio OpenAI-kompatibel ist. Ergänzt clientseitige TTFT-Messung:
```csharp
namespace Avaimi.Adapters.LmStudio;

public class LmStudioAdapter : ILlmAdapter
{
	public string ProtocolName => "LM Studio";
	public bool SupportsFileAttachments => false;

	private readonly OpenAiAdapter _inner;

	public LmStudioAdapter(string baseUrl, string? token = null)
	{
		_inner = new OpenAiAdapter(baseUrl, token);
	}

	public IAsyncEnumerable<LlmChunk> StreamAsync(LlmRequest request, CancellationToken ct = default)
		=> _inner.StreamAsync(request, ct); // OpenAI-Stream liefert bereits TtftMs

	public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
		=> _inner.CompleteAsync(request, ct);
}
```

### Integration in `BackendApiClient` / `ServerProfilesViewModel`

- `ServerProfilesViewModel` erhält eine Methode `GetMetadataProviderForProfile(ServerProfile p) : IMetadataProvider?`, die bei `LlmProtocol.LmStudio` einen `LmStudioMetadataProvider` zurückgibt, sonst `null`.
- In `ServerProfilesView.axaml` kann ein „System-Info"-Button eingeblendet werden, wenn `EditProtocol == LmStudio`.
- `BackendApiClient.GetServersAsync()` bleibt unverändert – Metadaten werden direkt vom Client zum LM Studio Server abgerufen (kein Backend-Roundtrip nötig).

### Akzeptanzkriterien
- `LmStudioMetadataProvider.GetAvailableModelsAsync` gibt Modell-IDs eines laufenden LM Studio Servers zurück.
- `GetServerMetadataAsync` gibt GPU/VRAM-Infos zurück, wenn `/v1/system_info` verfügbar ist, und gibt ein leeres `ServerMetadata`-Objekt zurück, wenn der Endpunkt nicht existiert (graceful degradation).
- `LmStudioAdapter` streamt Nachrichten korrekt und liefert Token-Zahlen aus dem letzten Stream-Chunk.
- Der Adapter ist von `OpenAiAdapter` getrennt und unabhängig testbar.

---

## Implementierungsreihenfolge

| Schritt | Feature | Hauptdateien |
|---|---|---|
| 1 | Umbenennung Sidebar-Header (F3) | `MainView.axaml` |
| 2 | Aktive Session hervorheben (F1) | `MainView.axaml`, `App.axaml` |
| 3 | Faltbare Sidebar (F2) | `MainViewModel.cs`, `MainView.axaml` |
| 4 | Skills-Tab + Sidebar-Button (F4) | `MainView.axaml`, `HistoryViewModel.cs`, `SkillsView.axaml`, `HistoryView.axaml` |
| 5 | Metadaten live pro Nachricht (F5) | `ChatSessionViewModel.cs`, `ChatSessionView.axaml` |
| 6 | Server-Typ-Dropdown (F6) | `ServerProfile.cs`, `ServerProfilesView.axaml`, `ServerProfilesViewModel.cs` |
| 7 | LM Studio Adapter (F7) | `AvalonianAiMiddleware.Adapters/LmStudio/` (neue Dateien) |

---

## Abhängigkeiten und Risiken

| Risiko | Betroffene Features | Maßnahme |
|---|---|---|
| `IsSidebarCollapsed` kann nicht per `GridLength`-Binding gesetzt werden | F2 | `IValueConverter<bool, GridLength>` implementieren |
| LM Studio `/v1/system_info` ist experimentell und nicht in allen Versionen vorhanden | F7 | Try/Catch + Fallback auf leeres `ServerMetadata` |
| `ShowMetadata`-Propagierung zu bestehenden Nachrichten fehlt | F5 | `OnShowMetadataChanged` iteriert `Messages` |
| `TabControl`-Index für Skills-Tab kann sich durch Tab-Reihenfolge verschieben | F4 | Named `TabItem` statt Index-basierter Navigation verwenden |
| Android/iOS-Projektfehler (`AvaloniaMainActivity`, `AvaloniaAppDelegate`) | alle | Pre-existing, nicht Teil von v0.4; ignorieren |
