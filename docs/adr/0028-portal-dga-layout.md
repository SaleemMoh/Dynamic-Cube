# ADR-0028: The portal follows the DGA layout of entity portals; page feedback and service ratings are stored

**Status:** Accepted

## Context
The reference design for a government entity portal built on the DGA design system was compared with ours, and ours
fell short. The reference has these pages:
- A home page: title band, search with a dark button, underline tabs, service cards with tags, a paged carousel and
  statistics.
- List pages with a title band, a filter panel and tables.
- A service page with a side card, related services, comments and suggestions, a rating and "Was this page helpful?".
- A form page with a contact card beside it.
- Dialogs.

## Decision
- **Shell (shared with the field app):**
  - An 80px header. The current section is filled with the main colour, with a light bar at the bottom.
  - The language is shown as "En" / "ع".
  - The user name has a chevron.
  - A full-width slot above the footer, which the portal fills with the last-modified line in Riyadh time.
  - The footer has underlined links, a bold copyright line, the entity logo in white and the footer image.
- **Title band (`dc-page-hero`)** on the home, services, requests, documents, FAQ and content pages:
  - The title is up to 64px, and the purpose is written under it.
  - The background is the entity's hero image under a dark green layer. Without an image, it is a glass-facade
    pattern.
  - The image is a new branding asset. The home setting's banner address still overrides it.
- **Portal building blocks** (portal styles only, so the internal system keeps its own guide):
  - Rectangular buttons and 48px fields.
  - Section heads with a lead line and "View all".
  - Underline tabs.
  - Tags in neutral, information and success colours.
  - A search field with a dark button. On the home page its menu searches one category.
  - Square arrow buttons.
  - Breadcrumbs.
  - Filter panels over 64px table rows.
- **Cards:** an icon in a green circle, a two-line description, tags (audience, category, free) and a square arrow.
  The home carousel moves by whole views, with arrows and page dots.
- **Service page:**
  - The start button sits beside the title.
  - Underline tabs: steps, terms, documents and levels.
  - A bordered side card: audience, duration, channels, cost, then FAQ, phone and email links.
  - A light green band of related services (the same category).
  - The comments and suggestions card, the rating and "Was this page helpful?".
- **Request form:** a breadcrumb, then the wizard beside the entity's contact card. The card holds the phone and
  email (copied with one click), the location and the social accounts.
- **Entity contact details** are part of branding: phone, email, location in both languages, a map link and up to 8
  social accounts from a fixed list of networks. They are validated, change through the system area's change set,
  and are public in `/branding`. The home setting's help phone and email still take precedence.
- **"Was this page helpful?"** is stored in the Content module (`cms.page_feedback`), with one answer per page and
  visitor. Signed-in users answer by their subject. Other visitors answer by a random id their browser keeps, and
  may change their answer.
- **Service ratings** (`cms.service_rating`) are one to five stars from signed-in users, one per user and service;
  rating again replaces the stars. Anyone may read the average and the count.
- The content team sees both under *Portal content → Visitor feedback*.

## Consequences
- The portal matches the guide's page anatomy without changing the internal system's look.
- Anonymous answers can be repeated by clearing browser storage. They measure sentiment; they are not a vote.
  Ratings require sign-in.
- Attachments in forms, a modal for "new request" and rating comments are not part of this change. Attachments still
  wait for the file runtime.
