# ADR-0049: Logic Builder

**Status:** Accepted (asked for by the owner: "JSONLogic is too complicated for normal users"; implemented). The owner
decided: the raw JSON stays available **behind an *Advanced* button**; the first version covers **conditions and
calculations**.

## Context
Rules are JSONLogic everywhere in the platform: eligibility, validations, fees and their amounts, levels'
recommend-when, form rules (shown when, required when), workflow routes and assignments, decision rule sets, automation
rules, notification conditions, list filters, calculator prefill. The visual rule editor of the designers (ADR-0023)
handled only a flat list of comparisons and a field or fixed value; anything else, and every rule in configuration
screens and table cells, was typed as JSON.

## Decision

### One model of a rule
- `logic-model.ts` reads any JSONLogic into a tree and writes it back: **conditions** (groups of *all* / *any*, negated
  or not, nested; comparisons; *some / every / no row* of a list meets a condition) and **values** (a field, a fixed
  value, `+ − × ÷ %`, smallest / largest, *if … then … otherwise*, the sum of a column or the number of rows of a list
  — optionally only the rows meeting a condition — joined texts, yes/no from a condition).
- What the form cannot show stays a **raw piece** in its place, edited as JSON, so reading and writing a rule never
  changes what it does. Every rule of the NHC demo (225) is shown fully in the form and written back unchanged
  (`{"!": x}` is written as `{"!": [x]}`, which means the same).
- Comparisons write the forms both evaluators (browser and server) already run: `==`, `!=`, `>`, `>=`, `<`, `<=`,
  between (`<=` with three arguments), one of / not one of (`in`, `!`), contains (`in` with the field second), starts
  with (`substr`), filled / empty (`!!`, `!`), yes / no (`== true/false`, or the field alone).

### The Logic Builder (`dc-logic-builder`)
- A **field menu** grouped by where facts come from (Answers, Beneficiary and applicant, Task, Decision results,
  Staff inputs…), searched by typing; a path that is not listed can still be typed.
- **Comparisons by field type** (text, number, date, yes/no, list, rows of a list) and **values by the field's
  choices**: its options, the values of its **class** (Class/Domain) or of its **list of values**, loaded once per
  page; for "one of" several values are picked. The right side can be switched to another field.
- **Calculations** chosen from a *kind of value* menu; each operand is itself a value of any kind.
- The rule **in words** under the form ("Region is Makkah Region", "Sum of Contract value of Completed projects is
  more than 30"), **Try it** with sample values (evaluated in the browser, nothing saved) and **Advanced** (the JSON,
  checked as it is typed).
- `dc-rule-editor` (used by the Service Builder, the Workflow Designer, task fields, the notification rule editor) is
  now the Logic Builder under its old name, so every one of those places uses it without change.
- `dc-logic-chip`: a rule shown as its sentence in a small space (a table cell); clicking it edits the rule in the
  Logic Builder in a dialog.

### Fields: catalogs and contexts
- A field (`LogicField`) has a path, a name, a type, and its choices: `options`, `classCode` or `lovCode`; a *rows*
  field has its `columns`. Questions become fields with `answerField()` (type, list of values, row columns).
- Configuration screens point the builder at a **named context** registered by the app (`LogicContexts`):
  `task` (automation rules), `classValue` (a list's static filter: the value, its parent, the user's roles, org units,
  company, app), `party` (a calculator's prefill: party kind and profile fields typed by their data type). A table
  cell can also read the other rows of its table (rule set steps as `vars.CODE`, a row's sibling columns).
- The structured editor of configuration screens gained a `logic` shape (the whole field is a rule), `logic` columns
  (chips) and maps whose values are rules (a rule set's results).

### Where it is used
Service Builder (eligibility, validations, fees and amounts, levels, form rules, side panel), Workflow Designer (routes,
assignment rules, mappings, task fields, items, per-element documents), notification rules, automation rules, rule set
versions (steps, elements, results), repeating-rows columns (shown when, required when), list static filters,
calculator prefill.

## Consequences
- Makers write rules by choosing; codes of class and list values are chosen by name; the sentence shows what a rule
  means before it is saved.
- Stored rules are unchanged JSONLogic: no migration, packages and the server are untouched.
- The old flat rule model (`rule-model.ts`) is removed.

Later: fields over registered tables and their columns where a rule reads table rows (the query builder and report
filters keep their own condition steps today), date functions (today, days between) once both evaluators have them,
named reusable conditions, a rule's sentence in change set reviews.
