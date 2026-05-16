# Feature Spec v0.4.1

## Übersicht

Release 0.4.1 behebt drei Usability-Probleme des Releases 0.4.

---

## Feature 1 – Aktive Session farbig hervorheben (Sidebar)

### Problem
Die aktive Chat-Session im linksseitigen Menü wird nicht farbig hervorgehoben. Der CSS-class-Selektor `.active` (in App.axaml) wird von der inline gesetzten Property `Background="Transparent"` überschrieben, da Inline-Properties in Avalonia höhere Priorität als Styles haben.

### Lösung
- Neue CSS-Klasse `session-btn` für alle Session-Buttons in der Sidebar.
- In `App.axaml`: Stil für `Button.session-btn` definiert (transparenter Hintergrund, volle Breite).
- In `App.axaml`: Stil für `Button.session-btn.active` definiert (Akzentfarbe + weißer Text + fett).
- In `MainView.axaml`: Inline `Background="Transparent"` von Session-Buttons entfernt; stattdessen `Classes="session-btn"`.
- Die vorhandene Binding `Classes.active="{Binding IsActive}"` bleibt erhalten und greift nun korrekt.

### Akzeptanzkriterien
- Die aktive Session hebt sich deutlich mit Akzentfarbe ab.
- Inaktive Sessions sehen aus wie bisher (transparenter Hintergrund).

---

## Feature 2 – Historie: Master-Detail-Layout analog zu Skills

### Problem
Die Historienansicht zeigt alle Einträge als lange Liste mit inline gesetzten Aktionsbuttons – unübersichtlich und inkonsistent mit dem Skills-Layout.

### Lösung
- `HistoryViewModel`: neue Property `SelectedSession` (Typ `SessionSummaryViewModel?`).
- `HistoryView.axaml`: Umbau auf `Grid ColumnDefinitions="250,Auto,*"`:
  - **Links (250px)**: `ListBox` mit Sitzungsliste (Titel + Datum), `SelectedItem` gebunden an `SelectedSession`.
  - **Mitte**: `GridSplitter`.
  - **Rechts**: Detailpanel für die ausgewählte Session:
	- Vollständige Metadaten (Titel, Kommentar, Thema, Modell, Nachrichten, Datum).
	- Alle Aktionsbuttons: Öffnen, JSON, Markdown, → Skill, ✎ Bearbeiten, 🗑 Löschen.
	- Inline-Editformular (war zuvor in der Listzeile, jetzt im Detailpanel).
	- Platzhaltertext wenn nichts ausgewählt.

### Akzeptanzkriterien
- Linke Liste zeigt kompakten Überblick (Titel + Datum).
- Rechtes Panel zeigt alle Details und Aktionen für die ausgewählte Session.
- Inline-Editformular öffnet sich im Detailpanel.

---

## Feature 3 – Chat-Metadaten separat anzeigen

### Problem
Die technischen Metadaten (Server, Modell, TTFT, Gesamtzeit, Token) werden direkt unter jeder Assistenten-Nachricht als Zeile angezeigt. Das verschmutzt den Chat-Verlauf. Außerdem sind sie nur in der offenen Session sichtbar, nicht in der Historie.

### Lösung

#### Datenmodell (kein Umbau der Persistenz nötig)
- `ChatSessionViewModel` erhält Property `SessionMetadata` (neues inneres Model):
  ```
  Server, Modell, TTFT, TotalMs, InputTokens, OutputTokens, Timestamp
  ```
- Nach jeder Assistenten-Antwort (und beim Laden der Historie) wird `SessionMetadata` auf die Werte der **letzten** Assistenten-Nachricht gesetzt.
- `SessionMetadata` ist eine `ObservableObject`-Klasse → live-aktuell in der UI.

#### UI
- In `ChatSessionView.axaml`: Per-Message-Metadaten-Textzeile (`MetadataText`) wird **entfernt**.
- Stattdessen: separates Metadaten-Panel zwischen Nachrichtenliste und Statuszeile:
  - Sichtbar gesteuert durch vorhandenen Toggle `ShowMetadata`.
  - Zeigt: Server, Modell, letzter TTFT, letzter Total, Input/Output-Tokens, Zeitstempel.
  - Erscheint als kompakte, abgegrenzte Border, damit der Chat sauber bleibt.
- Der `Toggle-Metadata`-Button (✦) in der Toolbar bleibt erhalten und steuert weiterhin das Panel.

### Akzeptanzkriterien
- Chat-Bubbles zeigen ausschließlich den Nachrichteninhalt.
- Das Metadaten-Panel zeigt immer die aktuellsten Werte der Session.
- Panel ist per Button ein-/ausblendbar.
- Nach dem Laden einer Session aus der Historie werden die Metadaten korrekt aus der letzten Assistenten-Nachricht befüllt.

---

## Betroffene Dateien

| Datei | Änderung |
|---|---|
| `App.axaml` | Neue Styles `session-btn`, `session-btn.active` |
| `Views/MainView.axaml` | Session-Button-Klassen |
| `ViewModels/HistoryViewModel.cs` | `SelectedSession` Property |
| `Views/HistoryView.axaml` | Master-Detail-Umbau |
| `ViewModels/ChatSessionViewModel.cs` | `SessionMetadata` Property, Update-Logik |
| `Views/ChatSessionView.axaml` | Metadaten-Panel, Per-Message-Metadaten entfernen |
