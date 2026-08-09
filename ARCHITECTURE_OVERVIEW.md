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
- Links befindet sich der Baum, der die JSON‑Struktur zeigt; rechts wird der Inhalt angezeigt (Editorbereich). Im Editorbereich soll die aktuelle Hilfestellung zum fokussierten UI-Element angezeigt werden. Die Werte $id und $schema werden verdeckt angezeigt, also nicht sofort sichtbar.

 
#### Baumansicht (links)
- Zeigt die JSON‑Struktur.
- Hinter jedem Element wird die Anzahl der Unterelemente angezeigt, z. B. `{6}` oder `[9]`.
- Einzelelemente werden nicht aufgeklappt, da sie im Editorbereich sichtbar sind.
- Array‑Objekte werden als `[0]`, `[1]` usw. dargestellt – aber nur, wenn das jeweilige Array-Element selbst weitere navigierbare JSON-Strukturen (Objekte oder Arrays) enthält. Enthält ein Array-Element ausschließlich Scalar-Eigenschaften, wird es nicht als eigener Baumknoten dargestellt, da seine Inhalte vollständig im Editorbereich des Arrays bearbeitet werden.
- Pfeile zeigen den Aufklappstatus:
  - `▶` für zugeklappt
  - `▼` für aufgeklappt
  - `[]` für Arrays
- Über der Baumansicht befindet sich ein Suchfeld.
- Im Baum kann man nur zu einem JSON-Objekt oder einem tatsächlich navigierbaren Array-Kontext springen. Einzelelemente sollen nicht mit Einzelklick aufgeklappt werden können, da man diese bereits im Editorbereich sieht. Ein Aufklappen mit Einfachklick ist nur möglich, wenn sich unter einem JSON-Objekt weitere navigierbare JSON-Objekte/Arrays befinden oder unter einem Array-Element weitere navigierbare JSON-Objekte/Arrays existieren. 
- Ein Array, dessen Elemente ausschließlich Scalar-Eigenschaften oder einfache Objekte ohne weitere verschachtelte Objekte/Arrays enthalten, bleibt als Array-Knoten im Baum sichtbar; seine einzelnen Elemente werden jedoch nicht als [0], [1] usw. im Baum dargestellt.
- Beispiel: matrixData → [[{x,y,value}, {x,y,value}], ...]: Die erste Array-Ebene matrixData ist navigierbar; die Array-Elemente [0], [1] der inneren Arrays werden nicht als Baumknoten benötigt, wenn deren Inhalte vollständig über die Array-Card-/Editor-Darstellung erreichbar sind.

#### Editorbereich (rechts)
- Zeigt die Inhalte des selektierten JSON‑Objekts.
- Oben wird ein Breadcrumb angezeigt (z. B. `root / plugins / [0]`).
- Ein Klick auf den Breadcrumb springt direkt an die entsprechende Stelle im Baum.

##### Editieren im Editorbereich (rechts)
- Alle Änderungen am jeweiligen JSON‑Teil müssen vom UI‑Control, das diesen Teil visualisiert, im Speicher mitverfolgt werden, um auf geänderte Daten den Benutzer hinzuweisen und ggf es rückgängig machen zu können.
- Ist im Baum keine Liste fokussiert, entfällt der vertikal geteilte Bereich, und es wird der Editorbereich über die gesamte Breite angezeigt.
- Das JEDES UI-Controll muss fokusierbar sein mit seinem Text und Knopf im gesamten. Der Fokus muss leicht für den Benutzer angedeutet sein, damit er den Bezug zur Hilfetellung erkennt.

#### Arrays im Editorbereich
- Arrays werden im Editorbereich als Array‑Item‑Cards angezeigt. Die Anzeige teilt den Editorbereich vertikal und links sind die Array‑Item‑Cards, rechts weiter der Editorbereich.
  - Diese Darstellung wird auch für tief verschachtelte JSON‑Objekte genutzt, die Arrays enthalten. In diesem Fall wird die Baumstruktur bis zu diesem Objekt visualisiert, und der Editorbereich zeigt ab dieser Ebene den Inhalt an. Wenn dort ein Array vorhanden ist, wird der Editorbereich nicht erneut  geteilt und die Ansicht wird nicht enger. Enthält ein Array einfache Objekt-Elemente ohne weitere navigierbare Unterstrukturen, werden diese Elemente ausschließlich als Cards dargestellt und nicht zusätzlich als [0], [1] usw. in der Baumansicht.
  - Bei verschachtelten Arrays bleibt die Baumselektion stabil; der Editorbereich zeigt die tiefere Ebene.
  - Jedes Card‑Item besitzt einen „Bearbeiten“‑Button zum Öffnen des Detail‑Editors und einen „Löschen“‑Button zum Entfernen des Items.
  - Unter der Liste befindet sich ein „Hinzufügen“‑Button, um neue Array‑Items anzulegen.
  - Wenn eine Liste selektiert ist, wird über der Liste die Hilfestellung aus dem JSON-Schema kompakt angezeigt.
  - Befindet sich im rechten Editorbereich wiederum eine Liste, erscheint auch hier ein Button, und alle drei Fensterbereiche fokusiere sich entsprechend auf diesen diesen Hierarchie-Abschnitt. Die Selektion im Baum bleibt dabei auf dem JSON-Objekt und wird nicht verändert.

### Temporäre Drilldown-Navigation innerhalb von Array-Items
- Array-Elemente wie [0], [1], [2] werden grundsätzlich nicht als dauerhafte Knoten in der Baumansicht dargestellt, da sie bereits durch die Array-Item-Cards im Editor repräsentiert werden.
- Enthält ein Array-Item ein verschachteltes JSON-Objekt oder Array, kann dieses aus dem Editorbereich heraus per Navigation/Drilldown geöffnet werden.
- Der Drilldown ist ein temporärer Editor-Kontext und erweitert nicht die dauerhafte Baumstruktur.
Beispiel: Bei root / plugins / [0] / settings bleibt die Baumselektion auf plugins. Der Breadcrumb zeigt dagegen den vollständigen Pfad inklusive [0] und settings.
- Wird innerhalb des Drilldowns ein weiteres Array geöffnet, ersetzt dessen Card-Liste die vorherige Array-Darstellung im Editorbereich. Der Editor wird dadurch nicht zusätzlich verschachtelt.
- Der Breadcrumb bildet bei jedem Drilldown den vollständigen effektiven JSON-Pfad ab, einschließlich der verwendeten Array-Indizes.
- Beim Verlassen des Drilldowns wird der vorherige Editor-/Array-Kontext wiederhergestellt.
- SelectedPath beschreibt weiterhin ausschließlich die fachliche Tree Selection. Der temporäre Drilldown-Kontext darf SelectedPath nicht verändern.
- Damit bleiben Tree Selection, Array-Card-Auswahl und temporärer Editor-Drilldown drei getrennte Zustände.

#### Hilfestellung
- Wird immer unten im Editorbereich angezeigt.
- Bleibt beim Scrollen sichtbar.
- Enthält:
  - `description`
  - `default`
  - `deprecated`
  - `readOnly`
  - `$comment`
- Die Hilfestellung gehört immer zum fokussierten UI-Control (das einen JSON-Teil visualisiert)
- Wenn nichts fokussiert ist, wird die Dokumentbeschreibung angezeigt.

#### Dark/Light‑Mode
- Umschaltbar über Icon.
- Im Dark‑Mode werden Rahmen neonfarben dargestellt.

#### Textmodus
- Aktivierbar über Icon und ist somit ein eigener Modus um JSON zu als Text zu editieren.
- Zeigt den JSON‑Text des selektierten Objekts im Baum.
- Baum bleibt sichtbar.
  - Die Selektion im Baum kann jederzeit gewechselt werden.
- Hilfestellung bleibt sichtbar und zeigt die Hilfe in Abhängigkeit, wo sich der Cursor im Editorfenster befindet.
- Validierungsfehler erscheinen unter dem Editor.
- Globale Fehler werden im Baum mit „!“ markiert.

#### Icon‑Streifen (unter der Baumstruktur)
Nur Icons, keine Textbuttons:

| Funktion | Icon |
|---------|------|
| Datei öffnen | 📂 |
| Datei speichern | 💾 |
| Schema laden | 🗂 |
| Dark/Light‑Mode | 🌓 |
| Textmodus | 📝 |
| Diff‑Ansicht | 🔀 |

### 2.3 Diff‑Ansicht
- Zwei JSON‑Configs nebeneinander.
- Änderungen farblich markiert.
- „Übernehmen“-Buttons pro Feld.
- Vergleich basiert auf JSON‑Baumstruktur.

### 2.4 Schema‑Auto‑Erkennung
- `$schema` im JSON wird automatisch erkannt.
- Falls nicht vorhanden:a
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
Der Core ConfixJson.Core enthält:

- JSON Parser (System.Text.Json)
- JSON Schema Parser (Json.Schema.Net)
- Validierungslogik
- Diff‑Engine
- Plugin‑System
- Undo/Redo
- Datei‑Handling
- Schema‑Auto‑Finder
- JSON‑Modelle (UI‑neutral)
  - JsonEditorNode (ohne ObservableProperty)
  - FieldRow (ohne ObservableProperty)
  - CardItem (ohne ObservableProperty)
  - NestedContext
  - SchemaFieldInfo
  - ValidationError
 - Services (bereits in Core)
  - JsonDocumentService
  - JsonDiffService
  - SchemaLoader
  - SchemaParser
  - SchemaValidator
  - UndoRedoService
 - Editor‑State
   ''''
    public class EditorState
    {
        public JsonNode Json { get; set; }
        public JsonNode OriginalJson { get; set; }
        public string[] SelectedPath { get; set; }
        public HashSet<string> Expanded { get; set; }
        public int? CardIndex { get; set; }
        public NestedContext? NestedCtx { get; set; }
        public ValidationError[] Errors { get; set; }
    }
 - Editor‑Logic
   - Alle Funktionen, die heute im Avalonia‑ViewModel liegen, aber keine UI‑Elemente benötigen:
   - Tree‑Builder
   - Node‑Selection
   - Expand/Collapse
   - Array‑Drilldown
   - Object‑Field‑Builder
   - Scalar‑Node‑Builder
   - Error‑Marking
   - Derived‑Values
   - JSON‑Manipulation
   - Undo/Redo‑Integration
   - Schema‑Integration

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
