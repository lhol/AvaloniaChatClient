Was wurde gebaut
Datei	Beschreibung
BackendApiClient.cs	HTTP-Client mit REST + SSE-Stream-Parser
ServerProfile.cs	DTOs: ServerProfile, LlmProtocol, TestConnectionResponse
ChatSession.cs	DTOs: ChatMessage, ChatSession, SessionSummary, SseChunk
Skill.cs	DTOs: SkillSummary, SkillContent
MainViewModel.cs	Shell-VM: Tab-Verwaltung, Session-Erstellung, Backend-Status
ServerProfilesViewModel.cs	CRUD für Server-Profile inkl. Verbindungstest
ChatSessionViewModel.cs	SSE-Streaming, Nachrichten-History, TTFT-Metriken
HistoryViewModel.cs	Session-/Skill-Verwaltung
MainView.axaml	Shell mit 3 Tabs + Neue-Session-Dialog
ServerProfilesView.axaml	Server-Profil-Liste mit Edit-Overlay
ChatSessionView.axaml	Chat-Bubble-Layout + Eingabebereich mit Stop-Button
HistoryView.axaml	Sessions & Skills mit Export/Löschen-Aktionen
Ablauf
1.	Tab „Server" – Profile anlegen/bearbeiten, Verbindung testen
2.	Tab „Chat" – Neue Session über ＋ Neue Session, mehre Sessions als Tabs, SSE-Streaming mit TTFT-Anzeige
3.	Tab „Historien" – gespeicherte Sessions öffnen/löschen, als Skill exportieren, Skills verwalten
