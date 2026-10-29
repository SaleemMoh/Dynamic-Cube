# ADR-0033: Forms instead of JSON entry, rich text for emails, employees in the org structure

**Status:** Accepted.

## Context
Many configuration fields hold nested data: a document type's data and line-item fields, an operation's inputs and
outputs, a visit type's outcomes, a user's assignments, a role's grants, a rule set's steps. They were edited as JSON
typed into a text box, which administrators should not have to write. Email bodies were raw HTML. The org structure
showed units but not who works in them.

## Decision
- **Structured input** (shared `dc-structured-input`): a field's `structure` says what its value holds — `table`
  (rows with typed columns: text, number, date, yes/no, choice, comma-separated tags, expression, nested table or
  object), `list` (texts or numbers, or a choice of options), `object` (fixed properties), `map` (free names and values)
  or `json`. It edits the value itself through a form for that shape; a *JSON* toggle shows the same value and accepts
  pasted JSON. Choice columns take options from other configuration types (`ref`) or classes (`refDomains`).
- Every `json` field of the configuration screens has a shape; the visit type, role and rule-set version editors use
  the same input. JSONLogic rules stay expressions (`value` cells, or `json` fields such as conditions), with the visual
  rule editor where one exists (workflow designer, notification rules).
- The configuration package template builds its examples and property help from the shapes.
- **Rich text** (shared `dc-rich-text-editor`): formatting toolbar, links, *Insert field* (`{{ path }}` from the
  event's sample) and an HTML view. Email bodies of message templates use it; stored as HTML, cleaned of scripts and
  event handlers before it is shown.
- **Org structure**: the selected unit lists the employees placed in it with their position (optionally with its
  sub-units), and employees are placed in or removed from the unit there, which changes their user row through a change
  set. Unit types and positions explain themselves on their tabs.

## Consequences
- Payloads and packages are unchanged: the forms produce the same JSON.
- New nested fields declare a shape instead of a JSON example hint.
