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
- Baumansicht für Objekte auf der linken Seite, rechts der Editorbereich.
- Im Editorbereich soll die aktuelle Hilfestellung zum fokussierten UI-Element angezeigt werden. st nichts fokussiert, wird die Beschreibung des gesamten Dokuments aus dem Schema angezeigt. Die Hilfestellung besteht aus den Feldern description, default, deprecated, readOnly und – falls vorhanden – $comment 
- Die Werte $id und $schema werden verdeckt angezeigt, also nicht sofort sichtbar.
- Links befindet sich der Baum, der die JSON‑Struktur zeigt; rechts wird der Inhalt angezeigt (Editorbereich).
  - Im Baum wird hinter jedem Element die Anzahl der Unterelemente (abgedunkelt) angezeigt, z. B. {6} oder [9].
  - Im Baum kann man nur zu einem JSON-Objekt oder einem Array springen. Einzelelemente sollen nicht aufgeklappt werden können, da man diese bereits im Editorbereich sieht. Ein Aufklappen ist nur möglich, wenn sich unter einem Objekt weitere Objekte befinden oder in einem Array-Objekt weitere Objekte existieren. Array-Objekte werden mit [0], [1] usw. als Knoten dargestellt – allerdings nur, wenn darunter weitere Objekte liegen.
  - Im Baum wird vor dem Element angezeigt durch den Pfeil nach rechts, ob aufgeklappt werden kann oder ein [] zeichen, das anzeigt das es ein Array ist. Ist es aufgeklapt, zeigt der Pfeil nach unten
  - Über der Baumansicht ist ein Suchfeld
  - Im Editorbereich wird oben ein Breadcrumb angezeigt, der die Position im Baum widerspiegelt (z. B. root / plugins / [0]). Ein Klick darauf springt direkt an die entsprechende Stelle in der Baumstruktur. 
- Arrays werden im Editorbereich als Array‑Item‑Cards in einer nebeneinander geteilten Ansicht dargestellt, anstatt als „Array‑Item‑Nodes“ in der Baumstruktur. Das gilt auch für tief verschachtelte JSON‑Objekte, die Arrays enthalten. In diesem Fall wird die Baumstruktur bis zu diesem Objekt visualisiert, und der Editorbereich zeigt ab dieser Ebene den Inhalt an. Wenn dort ein Array vorhanden ist, wird der Editorbereich nicht erneut  geteilt und die Ansicht wird nicht enger..
  - Jedes Card‑Item besitzt einen „Bearbeiten“‑Button zum Öffnen des Detail‑Editors und einen „Löschen“‑Button zum Entfernen des Items.
  - Unter der Liste befindet sich ein „Hinzufügen“‑Button, um neue Array‑Items anzulegen.
  - Wenn eine Liste selektiert ist, wird über der Liste die Hilfestellung aus dem JSON-Schema kompakt angezeigt.
  - Befindet sich im rechten Detail‑Editorbereich wiederum eine Liste, erscheint auch hier ein Button, und alle drei Fensterbereiche fokusiere sich entsprechend auf diesen diesen Hierarchie-Abschnitt. Die Selektion im Baum bleibt dabei auf dem JSON-Objekt und wird nicht verändert.
- Alle Änderungen am jeweiligen JSON‑Teil müssen vom UI‑Control, das diesen Teil visualisiert, im Speicher mitverfolgt werden, um auf geänderte Daten den Benutzer hinzuweisen und ggf es rückgängig machen zu können.
- Die UI soll einen Dark‑ und Light‑Mode besitzen. Im Dark‑Mode sollen beispielsweise Rahmen in Neonfarben gestaltet sein, um ein modernes Design zu erzeugen.
- Die Hilfestellung zum fokussierten UI-Control (das einen JSON-Teil visualisiert) wird immer ganz unten im rechten Detail-Editorbereich angezeigt – kompakt, aber vollständig mit allen Hilfselementen aus dem JSON-Schema. Sie bleibt auch beim Scrollen im Editorbereich sichtbar, da nur der darüberliegende Bereich mit den UI-Controls gescrollt wird.
- Ist im Baum keine Liste fokussiert, entfällt der vertikal geteilte Bereich, und es wird nur der Detail‑Editorbereich über die gesamte Breite angezeigt. 
- Unter der Baumstruktur ist ein schmaler Streifen für Icons und Schaltflächen vorgesehen. Dort können der Dark‑Mode umgeschaltet, der Editorbereich in den Textmodus gewechselt, die Datei gespeichert oder die Diff‑Ansicht aktiviert werden.
- Der Texteditor ist ein eigener Modus und wird über einen Button aktiviert. Er zeigt nur für das aktuell selektierte Objekt den Teil-JSON als Text an und ersetzt den mit UI-Elementen visualisierten Editorbereich. Um die gesamte JSON-Datei zu sehen, muss auf das oberste Element im Baum selektiert werden. Die Baumstruktur bleibt dabei immer sichtbar, und die Selektion kann jederzeit gewechselt werden, um den entsprechenden JSON-Teil anzuzeigen.
   - Unter dem Editorbereich wird weiterhin kompakt die Hilfestellung aus dem Schema angezeigt – abhängig davon, wo sich der Cursor im Editorfenster befindet.
   - Eine oder zwei Zeilen darüber erscheinen mögliche Validierungsfehler. Diese beziehen sich auf den selektierten Teil im Baum. Globale Fehler werden in der Baumstruktur mit einem „!“ markiert.


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
