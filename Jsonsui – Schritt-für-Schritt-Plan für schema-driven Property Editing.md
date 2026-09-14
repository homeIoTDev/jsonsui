# Jsonsui – Schritt-für-Schritt-Plan für schema-driven Property Editing

## Ziel

Der Editor soll es ermöglichen, JSON-Properties im Editorbereich zu:

- erstellen
- löschen
- umbenennen
- später verschieben

Dabei gilt:

- Der Tree bleibt ausschließlich Navigation.
- Strukturänderungen passieren im Editorbereich.
- Object-Kontexte erhalten `+ Add property`.
- Array-Kontexte behalten die bestehende `+ Add item`-/Card-Bedienung.
- Array-Elemente, die Objekte sind, werden weiterhin über den bestehenden Drilldown geöffnet.
- Schema-Informationen bestimmen bevorzugt die möglichen Properties und deren Typen.
- Custom Properties bleiben möglich, sofern das Schema sie nicht ausdrücklich verbietet.
- Validation bleibt unabhängig davon bestehen und darf ungültige Zwischenzustände anzeigen.
- Jeder Implementierungsschritt muss separat buildbar und testbar sein.

---

# Phase 0 – Architektur und bestehendes Verhalten einfrieren

## Schritt 0.1 – Bestehende Architektur analysieren

Vor Änderungen zunächst analysieren:

- `Jsonsui.Core.Models`
- `EditorLogic`
- `EditorState`
- Schema-Modelle
- `SchemaParser`
- `SchemaFieldInfo`
- `FieldRow`
- `MainWindowViewModel`
- Object-/Array-Editor
- bestehende Drilldown-/`NestedContext`-Logik
- bestehende Add-/Remove-Logik für Arrays
- bestehende Validation

Keine Implementierung in diesem Schritt.

Ziel:

Dokumentieren, wo aktuell JSON-Mutationen durchgeführt werden und welche Methoden bereits für Array Add/Remove existieren.

## Schritt 0.2 – Baseline testen

Ausführen:

1. Build
2. App starten
3. `test_min_more.json` laden
4. `project`, `server`, `database`, `auth`, `plugins`, `logging` navigieren
5. Array-Drilldown testen
6. Temporal Editor testen
7. NumericUpDown testen
8. Validation testen
9. Schema HelpStrip testen
10. keine Exceptions

Erst danach mit der nächsten Phase beginnen.

---

# Phase 1 – Gemeinsames Editor-Visual-Pattern

## Ziel

Zukünftige Editoren sollen nicht jedes Mal eigene Lösungen für zusätzliche Buttons, Fokusrahmen und Fehlerdarstellung benötigen.

Nicht sofort einen Universal-Editor bauen.

Stattdessen ein kleines gemeinsames Konzept definieren:

```text
FieldRow
 ├── Label
 └── EditorVisual
       ├── LeadingContent
       ├── Editor
       └── TrailingContent
```

## Anforderungen

Das Pattern muss ermöglichen:

- normales TextBox-Editing
- NumericUpDown
- ComboBox
- TemporalFieldEditor
- optionale Buttons am Anfang
- optionale Buttons am Ende
- `HasErrors`
- äußerer Focus-State
- ReadOnly-State

Beispiele:

```text
Date:
[calendar][ TextBox ]

DateTime:
[calendar][clock][ TextBox ]

NumericUpDown:
[ TextBox ][spin buttons ]

ComboBox:
[ ComboBox ][dropdown ]
```

## Wichtig

Die vorhandenen Fluent-Control-Templates nicht unnötig ersetzen.

Wenn ein Control intern eigene Bestandteile besitzt, insbesondere:

- `NumericUpDown`
- `TextBox`
- `ComboBox`

sollen bestehende Templates möglichst weiterverwendet und nur gezielt über Styles/Selectors ergänzt werden.

Ziel ist Wiederverwendbarkeit ohne vollständigen Theme-Neubau.

## Test

Alle bisherigen Editor-Typen müssen unverändert funktionieren.

Insbesondere:

- NumericUpDown-Fokus bleibt stabil
- Temporal Buttons funktionieren
- rote Error-Borders funktionieren
- ReadOnly bleibt fokussierbar
- keine Regression beim TextBox-Fokus

---

# Phase 2 – Core: Property-Metadaten für Add

## Ziel

Der Core soll ermitteln können, welche Properties in einem Object-Kontext angelegt werden können.

Neue Core-Abstraktion definieren, z. B. konzeptionell:

```text
AvailablePropertyInfo
```

mit Informationen wie:

```text
Name
Type
IsRequired
IsAlreadyPresent
IsCustom
DefaultValue
SchemaInfo
CanAdd
```

Noch keine UI.

## Verhalten

Für:

```text
root
```

liefert Core die Properties, die laut Schema möglich sind.

Bereits vorhandene Properties dürfen nicht erneut angeboten werden.

Required-Properties müssen als solche erkennbar sein.

Defaultwerte müssen verfügbar sein.

## Custom Properties

Core muss zusätzlich feststellen können:

```text
CanAddCustomProperty
```

unter Berücksichtigung von:

```text
additionalProperties
```

Custom Property bedeutet:

> Property ist nicht im Schema definiert und wird generisch als JSON-Wert angelegt.

## Test

Unit-Tests für:

- Schema Property vorhanden
- Schema Property fehlt
- Required Property fehlt
- Default vorhanden
- Property bereits vorhanden
- Custom erlaubt
- Custom verboten
- Object ohne Schema

---

# Phase 3 – Core: Add Property

## Ziel

Die eigentliche JSON-Mutation gehört in den Core.

Konzeptionelle API:

```text
AddProperty(path, propertyName)
```

bzw. eine entsprechende bestehende Mutation-Architektur erweitern.

## Schema-first-Verhalten

Wenn `propertyName` aus dem Schema stammt:

- korrekten JSON-Typ erzeugen
- Default verwenden, falls vorhanden
- bei Object ein leeres Object erzeugen
- bei Array ein leeres Array erzeugen
- bei Boolean/Number/String einen sinnvollen initialen Wert verwenden
- Schema-Constraints nicht ignorieren

Beispiel:

```text
port
type = integer
default = 3000
```

ergibt:

```json
"port": 3000
```

## Custom

Bei Custom Property wird der Wert anhand eines generischen JSON-Typs erzeugt.

Noch keine UI.

## Test

Core-Tests für:

- String
- Number
- Boolean
- Object
- Array
- Default
- Required
- Custom
- verschachtelte Object-Pfade
- Array-Element-Object-Pfade

---

# Phase 4 – UI: Add Property im Object-Header

## Ziel

Jetzt erstmals UI.

Der Button kommt ausschließlich in den Editor-Header.

Bei:

```text
OBJECT project
```

wird:

```text
OBJECT project   ← →                 [+ Add property]
```

angezeigt.

Nicht im Tree.

Nicht zwischen den Property-Zeilen.

## Kontextabhängigkeit

Bei Object:

```text
+ Add property
```

Bei Array:

```text
+ Add item
```

Bei Scalar:

kein Add-Button.

## Add-Flyout

Klick auf `+ Add property` öffnet ein kleines Flyout direkt unter dem Button.

Schema-first:

```text
Add property

Schema properties
────────────────────────
★ name       string
★ port       integer
  timeout    integer
  tls        boolean
  database   object →

────────────────────────
+ Custom property
```

Bereits vorhandene Properties werden nicht angeboten.

## Test

Manuell:

1. Root auswählen
2. Add Property öffnen
3. Schema Properties prüfen
4. vorhandene Properties dürfen nicht erneut erscheinen
5. Required sichtbar
6. Default sichtbar
7. Property hinzufügen
8. UI aktualisiert sich
9. Tree zeigt das neue Property nur dann, wenn es aufgrund der bestehenden Tree-Regeln navigierbar ist
10. keine vollständige unnötige UI-Rebuild-Kaskade

---

# Phase 5 – Custom Property

## Ziel

Custom Properties explizit unterstützen.

Nur wenn Core meldet:

```text
CanAddCustomProperty == true
```

wird:

```text
+ Custom property
```

angeboten.

## Dialog/Flyout

Beispiel:

```text
Add custom property

Name
[________________]

Type
[string ▼]

Value
[________________]

[Cancel] [Add]
```

Mögliche Typen:

- string
- number
- boolean
- null
- object
- array

Object und Array werden zunächst leer erzeugt.

## Wichtig

Custom Properties verwenden weiterhin denselben Editor.

Es gibt keinen separaten "Custom JSON Editor".

## Test

- Custom String
- Custom Number
- Custom Boolean
- Custom Object
- Custom Array
- Custom Property anschließend bearbeiten
- Custom Property anschließend löschen
- Schema `additionalProperties: false`
- Schema ohne explizite Custom-Erlaubnis
- kein falsches Angebot von Custom Properties

---

# Phase 6 – Fehlende Required Properties

## Ziel

Jetzt die bereits bekannte Validation-Lücke mit dem neuen Add-System verbinden.

Wenn:

```text
server.port
```

fehlt und `port` required ist:

```text
server

host        localhost

⚠ port       required / missing
```

oder das fehlende Feld wird über den Add-Property-Bereich angeboten.

## Wichtig

Nicht versuchen, fehlende Properties im Tree künstlich sichtbar zu machen.

Der Tree bleibt Navigation.

Der Benutzer kann:

```text
server
```

öffnen und dort:

```text
+ Add property
```

verwenden.

Das vorhandene Error-Navigation-Konzept bleibt zusätzlich bestehen.

## Test

- Required Property fehlt
- Error Badge zeigt Fehler
- Error Navigator zeigt Fehler
- Navigation landet beim korrekten Parent-Kontext
- Add Property bietet das fehlende Required Property an
- Property hinzufügen
- Validation verschwindet
- Error Badge aktualisiert sich

---

# Phase 7 – Delete Property im Core

## Ziel

Property-Löschung als Core-Mutation.

Konzeptionell:

```text
RemoveProperty(path)
```

## Verhalten

Löschen eines normalen Properties:

```text
name → delete
```

entfernt den JSON-Eintrag.

Bei Required:

Löschen ist weiterhin möglich, sofern Jsonsui ungültige Zwischenzustände zulässt.

Danach muss Validation den Required-Fehler anzeigen.

Kein heimliches automatisches Wiederherstellen.

## Test

- normales Property löschen
- Required Property löschen
- Custom Property löschen
- verschachteltes Property löschen
- anschließend Validation
- Undo/Redo berücksichtigen, falls bereits vorhanden
- Tree/Editor-State bleiben konsistent

---

# Phase 8 – Delete UI

## Ziel

Property-Zeilen bekommen eine dezente Bearbeitungsmöglichkeit.

Kein permanentes `⋮` an jeder Zeile.

Bevorzugt:

- Hover zeigt Action-Button
- oder Kontextmenü per Rechtsklick

Menü:

```text
Rename
Move up
Move down
────────────
Delete
```

Bei Required:

```text
Delete property?

"port" is required by the schema.

Deleting it will make the document invalid.

[Cancel] [Delete]
```

Keine Löschung ohne explizite Bestätigung, wenn Required.

---

# Phase 9 – Rename

## Ziel

Doppelklick auf Property-Namen soll Umbenennen ermöglichen.

Schema-first-Regel:

Wenn Property durch das Schema definiert ist, nicht beliebig umbenennen.

Mögliche UX:

```text
Schema property:
Rename disabled / not applicable

Custom property:
Rename available
```

Bei Custom Properties:

```text
mySetting
```

→ Doppelklick

```text
[mySetting________]
```

Enter bestätigt.

Escape verwirft.

## Test

- Custom Property umbenennen
- Schema Property nicht unkontrolliert umbenennen
- Duplicate Names verhindern
- invalid JSON property names verhindern
- Escape/Enter
- Validation nach Rename

---

# Phase 10 – Verschieben von Object Properties

## Ziel

Zunächst KEIN Drag & Drop.

Implementieren:

```text
Move up
Move down
```

nur für Object Properties, sofern die interne JSON-Repräsentation die Reihenfolge erhalten soll.

Noch keine Hover-Einfügenadel.

## Begründung

JSON-Object-Reihenfolge ist normalerweise nicht semantisch relevant.

Die Funktion deshalb erst implementieren, wenn die anderen Struktur-Operationen stabil sind.

---

# Phase 11 – Array-Verhalten nicht zerstören

Bestehende Array-Bedienung bleibt erhalten.

Array:

```text
ARRAY plugins
```

→ `+ Add item`

Keine Umwandlung in `+ Add property`.

Wenn Array-Element ein Object ist:

```text
plugins[0]
```

bleibt der bestehende Drilldown erhalten.

Nach Drilldown:

```text
OBJECT plugins[0]                     [+ Add property]
```

Jetzt können Properties innerhalb des Array-Elements erzeugt/gelöscht werden.

Das ist ein zentraler Regression-Test.

---

# Phase 12 – Optional: Insert-Nadel für Arrays

Erst NACH den vorherigen Phasen untersuchen.

Für Arrays kann später eine Einfügeposition angezeigt werden:

```text
Card 0
────────────
     +
────────────
Card 1
```

Klick auf `+` öffnet nur die für dieses Array erlaubten Insert-Optionen.

Nicht global:

```text
string
number
boolean
object
array
```

sondern schema-driven.

Beispiel:

```text
plugins[]
items.type = object
```

→

```text
Insert plugin
```

Bei Array of strings:

```text
Insert tag
```

Bei mehreren erlaubten Typen:

```text
Insert
  String
  Number
  Object
```

Die Hover-Nadel darf nicht dauerhaft flackern.

Nur eine stabile Einfügeposition soll sichtbar werden, wenn der Mauszeiger tatsächlich über dem Zwischenraum liegt.

---

# Phase 13 – Editor-Accessory-System weiterverwenden

Während der gesamten Implementierung dürfen neue Controls nicht wieder eigene Speziallösungen für Button-Positionierung erfinden.

Für neue Editor-Funktionen zuerst prüfen:

```text
Kann der vorhandene EditorVisual/Accessory-Mechanismus verwendet werden?
```

Beispiele:

```text
TemporalFieldEditor
    Leading:
        Calendar
        Time

NumericUpDown
    Trailing:
        Spin Buttons

ComboBox
    Trailing:
        Dropdown

Future editor
    Leading/Trailing:
        custom action
```

## Grundregel

Der eigentliche Editor bleibt für Wertbearbeitung zuständig.

Accessories sind UI-Aktionen.

Beispiel:

```text
TemporalFieldEditor
    TextBox = Wert
    CalendarButton = Assistenz
    TimeButton = Assistenz
```

Die Buttons dürfen nicht die eigentliche Value-/Validation-Logik duplizieren.

---

# Teststrategie nach JEDEM Schritt

Nach jedem vertikalen Slice:

## 1. Build

```text
dotnet build Jsonsui.slnx
```

Keine Warnings/Errors.

## 2. Start

App starten.

## 3. Test-JSON laden

```text
test_min_more.json
```

## 4. Schema prüfen

Schema muss geladen sein.

## 5. Navigation

Mindestens:

```text
root
project
server
database
auth
plugins
logging
```

## 6. Object Editing

Add / Delete / Rename testen, sobald implementiert.

## 7. Array Editing

```text
plugins
plugins[0]
plugins[0]/settings
```

testen.

## 8. Validation

Nach jeder Mutation prüfen:

- Error Badge
- Error Navigator
- Field Error
- Required Error
- verschachtelte Fehler

## 9. Temporal Regression

Nach UI-/Field-Infrastrukturänderungen:

- date
- time
- date-time
- Sekunden
- Offset
- Z
- Calendar Button
- Time Button

testen.

## 10. NumericUpDown Regression

- Mouse
- Wheel
- Keyboard
- Spin
- manuelle Eingabe
- Validation
- Fokus

testen.

## 11. Keine Regression

Besonders prüfen:

- Tree bleibt Navigation-only
- Scalar-Felder erscheinen nicht im Tree
- Array-Drilldown bleibt erhalten
- keine unerwarteten UI-Rebuilds
- kein Fokusverlust
- keine Exceptions

---

# Empfohlene Commit-Grenzen

Nicht alles in einen Commit.

```text
1. refactor(editor): introduce reusable field accessory pattern

2. feat(core): expose schema-aware available properties

3. feat(core): add object property mutation

4. feat(editor): add schema-driven property menu

5. feat(editor): support custom properties

6. feat(core): add property removal

7. feat(editor): add property actions

8. feat(validation): surface missing required properties

9. feat(editor): support custom property rename

10. feat(editor): support object property ordering

11. test(editor): add structure editing regression coverage
```

Array Insert/Drag & Drop erst danach als eigene Features.

---

# Wichtigste Architekturregel

Die KI soll bei jedem Schritt diese Grenze einhalten:

```text
                 Core
                  │
       ┌──────────┼──────────┐
       │          │          │
    Schema     Mutation   Validation
       │          │          │
       └──────────┼──────────┘
                  │
             ViewModel
                  │
                  ▼
                  UI
```

Core entscheidet:

> Was ist erlaubt?  
> Was fehlt?  
> Was kann hinzugefügt werden?  
> Welcher Typ entsteht?  
> Was passiert bei der JSON-Mutation?

UI entscheidet:

> Wo befindet sich der Button?  
> Wie sieht das Flyout aus?  
> Wie wird Hover dargestellt?  
> Wie wird der Benutzer durch die Aktion geführt?

Der Tree bleibt:

> **Navigation – keine Struktur-Editing-Oberfläche.**

Und besonders wichtig: **Die KI soll pro Schritt nur einen vertikalen Slice implementieren, danach Build + Test durchführen und erst dann den nächsten Schritt beginnen. Keine Sammeländerung über Core, ViewModels und mehrere Controls hinweg ohne Zwischenprüfung.**