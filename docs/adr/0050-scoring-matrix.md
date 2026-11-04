# ADR-0050: Scoring matrix

**Status:** Accepted (the configurable classification engine of the NHCI review §6, built in steps CE-1 to CE-4 with
the owner's "go" for each). The owner decided: **one request with a result per sector**; the special ICT rules are
**configurable** (a formula cell), not a coded plug-in; the reviewed system's `[metric]` formulas are **converted to
JSONLogic** on import; **engine and screens first**. Later asks: points and grades of question-based criteria follow
the **question bank**, answers are **weighted** there (points or a grade), a calculation used by several sectors
(governance) can be **shared** and later made different for one sector, and cells can be **copied** from one sector to
another.

## Context
Classification grades a firm in several sectors on several tracks (technical, performance, governance, financial). The
reviewed system (docs/reviews/nhci-classification-review.md) holds this as a tree per rule set version: sectors →
tracks → groups → criteria → bands per size, with scales, gates, adjustments and project rules. Written as JSONLogic
rule sets (ADR-0014) it would be hundreds of rules nobody could read or maintain, and the April 2026 guide alone has
179 criteria and 1,062 bands. The platform needed an engine that holds the guide as configuration, is filled from what
the request already knows, and that a business user can change.

## Decision

### A decision engine `MATRIX`
- A new engine of the Decisions module beside JSONLogic, with the same contract (ADR-0014): rule set code and version,
  input, output, explanation; stateless; versioned and saved through change sets; exportable in packages.
- **A table:** rows are the things classified (sectors), columns the assessments (tracks). Each **cell** says how its
  row is scored on its column: `points` (criteria give points, groups add up — a mandatory group must reach its minimum
  — and the total goes through a scale to a grade), `worst` (criteria give grades, the worst counts), `formula` (a
  JSONLogic rule gives the grade; the ICT category rules are one), `same` (as another cell), `shared` (below) or
  `empty` (the column does not apply).
- **Criteria** score an input by `bands` (ranges, optionally different per firm size), `options` (points or a grade per
  answer) or `direct` (the value is the points); a criterion may apply only when a rule holds.
- **Around the table:** grades (best first), sizes, scales, gates (a grade capped or refused when a rule fails),
  adjustments (promotion, demotion), the projects that count, and how a row's grade is made from its columns (`worst`,
  `weighted`, `formula`).
- **Results:** per row and column the grade, score, pending (a staff input still empty) and limiting columns; per row
  the provisional, base, final and expected grade; an explanation down to every criterion's points.
- **Importer:** the reviewed system's guide export becomes a matrix (metrics, option sets, sizes, grades, scales,
  9 sectors × 4 tracks, criteria, bands, gates, adjustments, project rules). Fixed tests grade the April 2026 guide and
  version 372 exactly as the reviewed system did for its simulator runs and three applications.

### Inputs and where they come from
- Each **input** has a type (number, text, option, yes/no, date, list) and a **source**: `applicant` (a bank question),
  `reviewer` (a workflow task's field), `integration` (an operation's output, called with inputs from the request),
  `party` (the firm's facts), `derived` (JSONLogic over other inputs), `projects` (sum, largest, count or distinct of a
  column over the projects that count) or `given`. Any input may be changed by staff when allowed.
- **The matrix reads a request itself:** when a workflow decision step passes the request's facts (an empty input
  mapping), the engine finds every input in the answers, the task fields, the integration results (matched by
  operation) and the party. No input mapping has to be written.
- **Rows applied for:** an input (e.g. the sectors question) lists the rows to grade; each row says which answer
  selects it. Without it every active row is graded.
- **Output for the platform:** `items` — one per row graded, flat, with each column's grade and whether it waits on
  staff — read by the applicant's live result (ADR-0040), item decisions and the task inputs card; with one row its
  grades are also given at the top level.

### The question bank weighs the answers
- A bank question's answers (options) may carry **points** and/or a **grade**. A criterion scored on answers can
  **use the bank's weights**: the designer copies them into the cell when the version is saved, and the *Connections*
  tab warns when the bank changed since.
- The weights are taken off what applicants receive with the form (`QuestionSnapshot.Shown`).
- Choosing a bank question for an input takes its type, names, answers and limits; answers of a list of values are
  read from the list.

### Shared calculations, copy and paste
- **Shared calculation:** a named calculation (`definition.shared`) several cells link to (`kind: shared`). Changing
  it changes every linked cell; *Edit only this sector* gives one cell its own copy (detach), so a track common to all
  sectors today (governance) can differ for one sector tomorrow without retyping the others.
- **Copy and paste** on the grid's cell menu: *Paste copy* (an independent copy), *Paste as link* (shares the copied
  cell), *Share with sectors…* (choose the rows), and per column *Link identical cells*. Importing a guide links
  identical cells automatically (23 cells to 5 shared calculations in the April 2026 guide).
- Shared calculations nobody uses are dropped on save.

### Designer
- Rule set page of a `MATRIX` rule set: the grid (rows × columns, a summary per cell), the cell editor (groups,
  criteria, bands per size, answers from the bank), inputs with their sources (questions, task fields, call steps,
  form answers, party facts), grades, scales, gates, adjustments, sizes and projects; a **simulator** (inputs of the
  chosen sectors only, the results with the explanation); a **Connections** tab checking every link (missing
  questions, operations, task fields, answers no longer in the bank, bank weights changed).
- The workflow designer's decision step offers the matrix rule sets and opens the matrix.

### NHC demo (package 37)
The service *Contractor classification (scoring matrix)* (`CLM-…`) on the April 2026 guide: the sectors and their
questions from the bank (11 with weighted answers), data fetched from four mock sources (Qawaem financial statements,
the sector authority's workforce, the register's sector validity, Ifaa violations), the live result on the portal, then
the workflow grades with the matrix, the financial analyst enters the credit rating (the grades are worked out again)
and the general manager approves.

## Consequences
- Classification for any authority is configuration: a new guide is imported or built in the designer, not coded.
- The JSONLogic rule sets stay for everything else; the matrix uses JSONLogic for its formulas, rules and derived
  inputs, written through the Logic Builder (ADR-0049).
- A cell linked to a shared calculation is not edited on its own by mistake: the editor says it is shared and with
  whom, and detaching is an explicit choice.
- Later: version compare and where-used for matrices, the grade ladder on the request page, the reviewed system's three
  review lanes as one workflow, project rules over the firm's record lists in the designer.
