Program.cs                          ← DI, CORS, Route-Registrierung
appsettings.json                    ← Port 5100, DataDirectory
Models/
  ServerProfile.cs                  ← ServerProfile, Create/UpdateRequest, TestConnectionResponse
  ChatSession.cs                    ← ChatSession, ChatMessage, SessionSummary, SendMessageRequest, SseChunk
  Skill.cs                          ← SkillSummary, SkillContent
Services/
  ServerProfileService.cs           ← CRUD auf servers.json
  SessionService.cs                 ← CRUD auf sessions/{id}.json, Nachrichten anhängen
  SkillService.cs                   ← CRUD auf skills/{id}_titel.md
Endpoints/
  ServerEndpoints.cs                ← GET/POST/PUT/DELETE /servers + /servers/{id}/test
  SessionEndpoints.cs               ← GET/POST/DELETE /sessions + /sessions/{id}/export
  MessageEndpoints.cs               ← POST /sessions/{id}/messages (SSE-Stream)
  SkillEndpoints.cs                 ← GET/DELETE /skills