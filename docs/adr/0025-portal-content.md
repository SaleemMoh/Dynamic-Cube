# ADR-0025: Portal content is a module of pages and questions; the notification rule builder edits rules as they are stored

**Status:** Accepted (Phase 3c)

## Context
Phase 3c asks for two things without JSON. The first is a notification rule builder. Rules were edited in the generic
form, with the condition and recipients as JSON. The second is a Portal Content Manager covering the home banner,
categories, statistics tiles, FAQs, help links and pages.

Part of the portal content already existed. The home page reads the public setting `PORTAL_HOME` (hero, banner,
tiles, help links), edited as JSON in Apps Control. Categories are configured in Services. There was nowhere to keep
pages (terms of use, privacy, about) or frequently asked questions.

## Decision
- **A small Content module (schema `cms`) owns pages and questions.**
  - `cms.page`: the name is the title, the body is Markdown in Arabic and English, plus *show in footer* and an
    order. A page's address is its code in lower case with `-` for `_`, so `TERMS_OF_USE` is
    `/pages/terms-of-use`.
  - `cms.faq`: the name is the question, the answer is Markdown, plus an optional topic in both languages, an
    optional service code and an order.
  - Both are ordinary configuration (change area `cms.content`, resource `content.config`, maker-checker,
    packages).
  - The external host serves them anonymously: `GET /api/content/v1/pages`, `/pages/{slug}` and
    `/faqs?service=`. Inactive rows are not shown.
- **The service code on a question is not a reference.** Services live in another module, and a question may be
  written before its service is configured. The code is checked for shape only; the service page links to its
  questions when some exist.
- **The home page stays a setting.** `PORTAL_HOME` keeps its shape. The Content Manager edits it with a structured
  form: hero wording and banner, up to six tiles (counted by the platform or a fixed number, with icon and labels,
  reordered), and help links. A preview sits beside the form. It saves the setting through the ordinary change set.
  Categories stay in Services, where the service catalog is configured.
- **Markdown is a small, safe subset rendered in the browser:**
  - Supported: headings, paragraphs, lists, quotes, rules, bold, italic, code and links.
  - All text is escaped first. Links must be http(s), mailto, tel or site-relative, and other sites open in a new
    tab. Angular's sanitizer checks the result again.
  - The same renderer previews in the Content Manager and displays on the portal, so the preview is exact.
- **The footer combines branding and content.** The footer shows the branding's links, then the pages marked for
  the footer, then the questions. A content link replaces a branding link with the same name. Links inside the
  application are followed without reloading.
- **The notification rule builder edits the stored rule, field for field.** The builder has three parts:
  - *When*: the event, grouped by module, with an optional condition in the shared visual rule editor. The
    condition sees the event's sample facts.
  - *Who*: the applicant, named roles or employees, or a role or employee named by the event (e.g. `task.assignee`,
    `task.escalatedToRole`).
  - *What*: the template, only the channels the template has wording for, and the default language.

  Beside the form, the message is previewed on the event's sample facts as each chosen channel will send it.

## Consequences
- Pages and questions travel in packages like any configuration (`deploy/config-packages/portal-content.json`).
- Questions are grouped on the portal by topic, in their order, and searched in the browser. A large set of
  questions would need server-side search later.
- Editing `PORTAL_HOME` requires the Apps Control settings privileges. The Content Manager's menu is guarded by
  `content.config`.
- Rules whose condition the visual editor cannot show (e.g. `substr`) are still edited as JSON in the same place.
