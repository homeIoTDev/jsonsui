# JSON UI Editor – Projektübersicht & Architektur

## 1. Ziel des Projekts
Der JSON UI Editor ist ein plattformübergreifendes Tool (Windows + Linux), das JSON‑Konfigurationsdateien anhand eines JSON Schemas als **geführte UI** darstellt.  
Statt rohem Text bietet das Tool:

- Dropdowns für Enums  
- Checkboxen für Booleans  
- NumericUpDown für Zahlen  
- Listen mit Add/Remove  
- Panels für verschachtelte Objekte  
- Live‑Validierung  
- Hilfe‑Texte direkt am Feld  
- Diff‑Ansicht zwischen zwei JSON‑Configs  

Das Tool soll sowohl **lokal** als native App laufen (Avalonia UI) als auch später **im Web** (Blazor UI) für einen Config‑Server.

---

## 2. Hauptanforderungen

### 2.1 JSON Schema als UI‑Blueprint
- Das JSON Schema definiert die UI vollständig.
- Typen werden automatisch in UI‑Elemente übersetzt.
- `description` wird als Hilfetext angezeigt.
- `enum` wird als Dropdown dargestellt.
- `object` wird als Panel/Expander dargestellt.
- `array` wird als Liste mit Add/Remove dargestellt.
- `oneOf` / `anyOf` wird als Auswahl‑Panel dargestellt.

### 2.2 Geführte UI (PropertyGrid‑Style)
- Keine Textbearbeitung im Vordergrund.
- UI‑Elemente verhindern Fehler.
- Pflichtfelder werden markiert.
- Ungültige Eingaben werden ignoriert oder optional gelöscht.
- Live‑Validierung direkt am Feld.

### 2.3 Diff‑Ansicht
- Zwei JSON‑Configs nebeneinander.
- Änderungen farblich markiert.
- „Übernehmen“-Buttons pro Feld.
- Vergleich basiert auf JSON‑Baumstruktur.

### 2.4 Schema‑Auto‑Erkennung
- `$schema` im JSON wird automatisch erkannt.
- Falls nicht vorhanden:
  - User wählt Schema manuell.
  - Tool durchsucht Schema‑Ordner.
  - Tool kann Schema‑Server abfragen (später).

### 2.5 VS Code Integration
- VS Code Extension startet das Tool extern.
- Übergibt Datei‑Pfad an das Tool.
- Optional: Rückschreiben der Datei nach Bearbeitung.
- Ziel: „Open in JSON UI Editor“-Button in VS Code.

### 2.6 Plugin‑System (Vorbereitung)
Plugins sollen später:

- eigene UI‑Elemente hinzufügen  
- eigene Validatoren registrieren  
- eigene Panels hinzufügen  
- eigene Diff‑Strategien implementieren  
- Schema‑Erweiterungen bereitstellen  

Plugins sind DLLs und werden dynamisch geladen.

---

## 3. Architekturentscheidungen

### 3.1 UI‑Framework: Avalonia UI (Desktop)
**Begründung:**
- Sehr schnell (native Rendering)
- Cross‑Platform (Windows + Linux + macOS)
- XAML‑basiert (wie WPF, aber modern)
- Ideal für Tools
- Keine Browser‑Abhängigkeit
- Kein WebView2 (Blazor Hybrid wäre Windows‑only)
- Perfekt für dynamische UI‑Generierung

### 3.2 Web‑Framework (später): Blazor WebAssembly
**Begründung:**
- Web‑Version für Config‑Server
- Multi‑User‑Editing möglich
- Gleiche Logik wie Desktop‑Version
- UI kann später nachgebaut werden
- Läuft überall im Browser

### 3.3 Core‑Schicht (shared)
Der Core enthält:

- JSON Parser (System.Text.Json)
- JSON Schema Parser (Json.Schema.Net)
- Validierungslogik
- Diff‑Engine
- Plugin‑System
- Undo/Redo
- Datei‑Handling
- Schema‑Auto‑Finder

Der Core ist **UI‑unabhängig** und wird von Avalonia und Blazor gemeinsam genutzt.

---

## 4. Technische Leitplanken

### 4.1 Umgang mit ungültigem JSON
- Parser ist tolerant.
- Ungültige Felder werden markiert.
- User entscheidet:
  - ignorieren  
  - reparieren  
  - löschen  
- Tool bietet Auto‑Fix‑Vorschläge.

### 4.2 Performance
- Avalonia für Desktop → schnellster Ansatz.
- Blazor WebAssembly nur für Web‑Version.
- Core ist optimiert für große JSON‑Dateien.
- UI nutzt Virtualisierung für große Arrays.

### 4.3 Startverhalten
Desktop:
```bash
jsonui config.json
