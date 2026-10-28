# ADR-0018: A gold working look for the internal system

**Status:** Accepted (refines [ADR-0016](0016-dga-design-system.md) for the internal system)

## Context
The internal system is a working tool: staff review, approve and maintain records all day, so it favours fast
scanning, safe actions and one consistent look. The portal is a public government website and keeps the DGA
shell of ADR-0016 (government bar, 72px header, green footer).

## Decision
- **Gold is the accent, DGA is the system.** The internal app (`web/projects/internal/src/styles.scss`) layers
  its tokens over the shared DGA base: `--dynamic-cube-gold` (with `-600` hover, `-700` text and links,
  `-100`, `-50` tints) marks the primary action, the selection and focus; `--dynamic-cube-green` is the primary
  action inside dialogs; neutrals are the DGA greys `--dga-gray-900 … -50`; shapes and depth are
  `--shell-radius-sm/-/-lg` (4/8/12px), `--shell-shadow-sm/-md/-lg`, `--shell-focus` (3px gold ring) and
  `--shell-header-h` (64px). Token names carry no client name. Components use the tokens, never hex values.
- **One frame for every screen:** a 64px sticky top bar (menu toggle, system name, language, signed-in user),
  a 320px side navigation from the Apps Control menu, the breadcrumb drawn by the workspace from that menu, the
  page header (28px title, one-line subtitle, actions at the end with the primary last), then cards
  (`.dc-card`: white, gray-200 border, 12px radius, small shadow).
- **Side navigation** opens and closes from the top bar (remembered in the browser; over the page below
  992px, where it starts closed); each section opens and closes from its title (remembered too). The shared
  menu reads its colours from `--dc-nav-*` variables, so the portal keeps the DGA green.
- Tables: gray-50 header with 13px/600 labels, 14px rows with hairlines, gray-50 hover, the selected row
  (`.dc-row--selected`) on gold-50 with a 3px gold edge at the start. Dialogs: 12px radius, hairlines around the
  title and the actions, actions at the end. Buttons: 40px, 8px radius, 14px/600; disabled at half opacity.
- The font stays IBM Plex Sans Arabic for both languages (ADR-0016).

## Consequences
- An entity re-colours the internal system by changing the `--dynamic-cube-*` tokens only.
- New internal screens reuse the shared classes (`dc-page__header`, `dc-card`, `dc-table`, `dc-count`, `mono`)
  instead of restyling.
