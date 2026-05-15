# Verifikations- und Review-Konzept

## Motivation

Gesetzliche Vorgaben schreiben vor, dass bestimmte Verifikationsschritte von qualifizierten Mitarbeitern durchgeführt werden. Gleichzeitig zeigt die Praxis, dass Menschen bei repetitiven, gleichförmigen Review-Tätigkeiten rasch an Aufmerksamkeit und Kompetenz verlieren – sogenannte *Review-Fatigue*. Das vorliegende Konzept adressiert dieses Spannungsfeld durch drei Prinzipien:

1. **Sinnvolle menschliche Verantwortung**: Reviewer treffen echte Entscheidungen und gestalten aktiv.
2. **KI als Lernpartner**: KI liefert Vorschläge, erklärt Hintergründe und zeigt Lücken auf – der Reviewer lernt dabei.
3. **Messbare Review-Qualität**: Jeder menschliche Review wird im Nachgang mit dem KI-Benchmark verglichen, sodass eine kontinuierliche Qualitätskurve entsteht.

---

## Entwicklungs- und Verifikationsprozess

### Schritt 1 – Business Case → Planungs-KI

| | |
|---|---|
| **Eingang** | Freitextbeschreibung des Business Case (Stakeholder-Interview, Vision-Dokument) |
| **Akteur** | Planungs-KI (großes Reasoning-Modell, z. B. o3 / Claude 3.7 Sonnet) |
| **Ausgang** | Strukturierter Draft: Zielbild, Scope-Abgrenzung, erste Risikofaktoren |
| **Artefakt** | `docs/business-case-draft.md` |

---

### Schritt 2 – Anforderungserhebung durch KI

| | |
|---|---|
| **Eingang** | Business-Case-Draft (Schritt 1) |
| **Akteur** | Planungs-KI |
| **Ausgang** | Vollständiger Anforderungs-Draft |
| **Artefakt** | `docs/requirements/` |

#### 2.1 Strukturierungsregel für Anforderungen

Jede Functional Requirement wird genau **einer** der folgenden Kategorien zugeordnet:

| Kategorie | Beschreibung | Beispiel |
|---|---|---|
| **Business Rule** | Invariante oder Lebenszyklusregel eines Geschäftsobjekts | „Ein Auftrag darf nur storniert werden, wenn er sich im Status *Offen* befindet." |
| **Use Case** | Interaktionssequenz mit messbarem Nutzen, ≥ 3 Schritte | UC-01 Auftrag anlegen |
| **Design Constraint** | Technische/regulatorische Rahmenbedingung | „Alle PII-Daten müssen AES-256-verschlüsselt gespeichert werden." |

Use Cases werden nach dem **RUP-Briefformat** (Primary Actor, Precondition, Main Flow, Extensions) dokumentiert.

---

### Schritt 3 – Review durch Business-Experten (Mensch, Pflicht)

> **Rechtlicher Hinweis**: Dieser Schritt ist durch [einschlägige Norm / internen Prozess] als menschliche Tätigkeit vorgeschrieben und darf nicht vollständig an KI delegiert werden.

**Ziel des Reviews**:  
Der Business-Experte soll *verstehen*, nicht nur *abnicken*. Dazu wird ihm folgende Arbeitsstruktur empfohlen:

#### Reviewbogen (je Anforderung auszufüllen)

| Kriterium | Frage | Skala |
|---|---|---|
| Vollständigkeit | Sind alle relevanten Ausnahmefälle abgedeckt? | 1–5 |
| Eindeutigkeit | Kann die Anforderung genau so implementiert werden? | 1–5 |
| Testbarkeit | Lässt sich ein Akzeptanztest formulieren? | 1–5 |
| Geschäftlicher Wert | Unterstützt die Anforderung ein definiertes Geschäftsziel? | 1–5 |

**Kreativaufgabe für den Reviewer** (verhindert Passivität):  
Für mindestens 20 % der Anforderungen formuliert der Reviewer selbstständig einen **Gegenbeispiel-Test** (ein Szenario, das die Anforderung verletzt). Diese Tests fließen später direkt in die Testsuite ein.

---

### Schritt 4 – Paralleles KI-Benchmark-Review

| | |
|---|---|
| **Eingang** | Anforderungs-Draft (Schritt 2) |
| **Akteur** | KI-Reviewer-Modell (maximales Skill-Set, z. B. o3 mit Prompt-Skill „Requirements-Quality") |
| **Ausgang** | Kommentierter Draft mit kategorisierten Verbesserungsvorschlägen |
| **Artefakt** | `docs/requirements/ai-review-v1.md` |

Die KI kennzeichnet jeden Vorschlag mit:
- 🔴 **Kritisch** – fehlende Anforderung, regulatorische Lücke
- 🟡 **Verbesserung** – Präzision, Konsistenz, Testbarkeit
- 🟢 **Ergänzung** – optionale Qualitätssteigerung

---

### Schritt 5 – Einarbeitung durch Business-Experten (Mensch, Pflicht)

Der Business-Experte erhält:
1. Seinen eigenen Review-Bogen (Schritt 3)
2. Den KI-Benchmark-Review (Schritt 4)
3. Eine automatisch generierte **Gap-Analyse**: Welche Punkte hat der Mensch übersehen, welche hat die KI übersehen?

**Arbeitsregel**:  
- 🔴-Punkte müssen vom Business-Experten **begründet entschieden** werden (akzeptieren / ablehnen mit Begründung).
- 🟡/🟢-Punkte darf er mithilfe kleiner KI-Modelle formulieren, muss sie aber **aktiv umformulieren** (kein reines Copy & Paste).

**Lernmechanismus**:  
Das System berechnet einen *Review-Score*: Anteil der von der KI gefundenen 🔴-Punkte, die der Mensch unabhängig identifiziert hatte. Dieser Score wird über Zeit getrackt und dient als Qualitätskurve für das Review-Team.

---

### Schritt 6 – Softwarearchitektur und finale Spezifikationen durch KI

| | |
|---|---|
| **Eingang** | Abgenommene Anforderungen (Schritt 5) |
| **Akteur** | KI-Programmiermodell + Architektur-Skills |
| **Ausgang** | Architektur-Dokument, ADRs, finale Spezifikationen |
| **Artefakt** | `docs/architecture/`, `docs/adr/` |

**Eingesetzte Skills (Prompt-Bibliothek)**:
- `skill-security-requirements` – OWASP Top 10, DSGVO-Mapping, Threat-Modell
- `skill-api-design` – REST/gRPC-Konventionen, Versionierungsstrategie
- `skill-scalability` – Lastannahmen, Skalierungsmuster
- `skill-testability` – Testpyramide, Observability-Anforderungen

---

### Schritt 7 – Review der Softwarearchitektur (analog Schritte 3–5)

Gleiches Vorgehen wie Schritte 3–5, jedoch mit einem **Architektur-Reviewer** (technischer Lead / Solution Architect).

**Zusätzliche Kreativaufgabe für den Architektur-Reviewer**:  
Er muss mindestens **ein Alternativszenario** (z. B. alternative Technologie, anderes Deployment-Modell) schriftlich bewerten und die getroffene Wahl begründen. Diese Entscheidung wird als **ADR (Architecture Decision Record)** festgehalten.

---

### Schritt 8 – Implementierung und Tests durch KI

| | |
|---|---|
| **Eingang** | Abgenommene Architektur und Spezifikationen (Schritt 7) |
| **Akteur** | KI-Programmiermodell + Coding-Skills |
| **Ausgang** | Produktionscode, Unit-/Integrations-/E2E-Tests |
| **Artefakt** | Source-Repository (getaggter Pre-Review-Stand) |

**Eingesetzte Skills**:
- `skill-secure-coding` – Input-Validation, Secrets-Handling, sichere Defaults, OWASP-Secure-Coding-Practices
- `skill-test-coverage` – Mindestabdeckung, Mutation-Test-Empfehlungen
- `skill-code-style` – Projekt-Konventionen, Linting-Regeln

---

### Schritt 9 – Code-Review (analog Schritte 3–5, mehrere spezialisierte Reviewer-Rollen)

Da Code-Reviews eine andere Fachtiefe erfordern als fachliche Reviews, werden **spezialisierte Reviewer-Rollen** eingesetzt. Jede Rolle erhält einen thematisch fokussierten Review-Auftrag, um Aufmerksamkeitsverlust durch zu breite Prüfaufgaben zu vermeiden:

| Rolle | Fokus | Kreativaufgabe |
|---|---|---|
| **Security Reviewer** (Pflicht, gesetzlich) | Sicherheitslücken, Datenschutz, sichere Konfiguration | Schreibt mindestens einen **Penetration-Test-Sketch** (Beschreibung eines Angriffsvektors + erwartetes Verhalten) |
| **Business-Logik Reviewer** | Korrektheit der fachlichen Umsetzung vs. Anforderungen | Formuliert mindestens 2 **Boundary-Value-Tests** für kritische Business Rules |
| **Architektur Reviewer** | Einhaltung der Architekturvorgaben, technische Schulden | Erstellt ein **Mini-ADR** für jeden Abweichungsfall |
| **Test Reviewer** | Vollständigkeit der Testsuite, Qualität der Assertions | Identifiziert und dokumentiert mindestens eine **Test-Gap** (nicht abgedeckter Pfad) |

#### Ablauf für jeden Reviewer

```
1. Reviewer erhält: Code-Diff, zugehörige Anforderungen/Architektur, KI-Review-Bericht
2. Reviewer führt seinen thematischen Review durch (Reviewbogen + Kreativaufgabe)
3. KI erstellt parallel einen spezialisierten Review-Bericht (passender Skill je Rolle)
4. Gap-Analyse: was hat der Mensch gefunden, was die KI, was beide?
5. Reviewer entscheidet über alle 🔴-Punkte (Pflicht) und 🟡-Punkte (Empfehlung)
6. Review-Score wird gespeichert → Lernkurve je Reviewer-Rolle
```

---

## Übergreifende KI-Unterstützungsmechanismen

### KI als Erklärer (verhindert Aufmerksamkeitsverlust)

Jeder Reviewer kann zu jedem Artefakt folgende KI-gestützte Aktionen auslösen:

| Aktion | Beschreibung |
|---|---|
| `Erkläre diese Komponente` | KI gibt eine verständliche Zusammenfassung inkl. Analogien |
| `Zeige ein Missbrauchsszenario` | KI demonstriert, wie der Code/die Anforderung ausgenutzt werden könnte |
| `Generiere Testfall für X` | KI erstellt einen konkreten Testfall, der manuell angepasst werden muss |
| `Vergleiche mit Best Practice` | KI vergleicht Implementierung mit State-of-the-Art und hebt Abweichungen hervor |
| `Simuliere einen Folgeauftrag` | KI spielt durch, wie eine zukünftige Anforderungsänderung diesen Code betreffen würde |

### Skill-Bibliothek (versioniert im Repository)

Alle eingesetzten KI-Prompts/Skills werden als versionierte Markdown-Dateien im Repository unter `docs/skills/` abgelegt. Damit ist nachvollziehbar, welche KI-Unterstützung in welchem Review-Schritt eingesetzt wurde (Audit-Trail).

```
docs/skills/
  skill-security-requirements.md
  skill-secure-coding.md
  skill-api-design.md
  skill-scalability.md
  skill-testability.md
  skill-test-coverage.md
  skill-code-style.md
  skill-requirements-quality.md
```

---

## Qualitäts- und Compliance-Metriken

| Metrik | Berechnung | Zielwert |
|---|---|---|
| **Human Review Score** | Anteil 🔴-Punkte, die Mensch unabhängig fand | ≥ 70 % nach 6 Monaten |
| **Review-Abdeckung** | Anforderungen mit vollständig ausgefülltem Reviewbogen | 100 % |
| **Kreativaufgaben-Rate** | Abgegebene Kreativaufgaben / geforderte | ≥ 95 % |
| **KI-Lückenrate** | 🔴-Punkte, die KI fand, Mensch aber nicht | Tracking (kein Zielwert, nur Trend) |
| **Test-Gap-Rate** | Gefundene Test-Gaps / Gesamtzahl reviewter Module | < 5 % |
| **ADR-Vollständigkeit** | Architekturentscheidungen mit ADR / Gesamt | 100 % |

---

## Rollenmatrix

| Schritt | KI-Modell | Mensch (Pflicht) | Mensch (optional) |
|---|---|---|---|
| 1 | Planungs-KI | – | Product Owner |
| 2 | Planungs-KI | – | Business Analyst |
| 3 | – | ✅ Business-Experte | – |
| 4 | KI-Reviewer | – | – |
| 5 | Kleines KI-Modell (Support) | ✅ Business-Experte | – |
| 6 | KI-Programmiermodell | – | Solution Architect |
| 7 | KI-Reviewer | ✅ Architektur-Reviewer | Solution Architect |
| 8 | KI-Programmiermodell | – | Tech Lead |
| 9a | KI (Security-Skill) | ✅ Security Reviewer | – |
| 9b | KI (Logik-Skill) | ✅ Business-Logik Reviewer | Business-Experte |
| 9c | KI (Architektur-Skill) | ✅ Architektur-Reviewer | Tech Lead |
| 9d | KI (Test-Skill) | ✅ Test Reviewer | QA Engineer |

---

## Werkzeugempfehlungen

| Bereich | Werkzeug / Ansatz |
|---|---|
| Review-Bogen & Scoring | Integriert in PR-Workflow (GitHub / Azure DevOps Custom Fields) |
| KI-Review-Reports | Automatisierte CI-Pipeline-Stage (KI-Review läuft bei jedem PR) |
| Gap-Analyse | Skript, das KI-Report und Human-Reviewbogen vergleicht |
| Skill-Versionierung | Markdown-Dateien im Repository, referenziert in CI-Stage |
| Review-Score-Dashboard | CSV/Dashboard (z. B. Power BI, Grafana) |
| Lernkurven-Feedback | Automatische Zusammenfassung nach jedem Sprint |

---

## Offene Punkte (zu klären)

- [ ] Welche konkreten gesetzlichen Normen/Standards definieren die Pflicht-Review-Schritte? (z. B. ISO 27001, IEC 62443, branchenspezifisch)
- [ ] Wie wird mit externen Reviewern (Auditoren) umgegangen – erhalten sie Zugang zu den KI-Review-Reports?
- [ ] Mindestkompetenznachweis für Reviewer: Gibt es ein Onboarding-Training für das KI-unterstützte Review-Verfahren?
- [ ] Datenschutz: Werden Code und Anforderungen an externe KI-APIs gesendet, oder wird ein On-Premise-Modell benötigt?
