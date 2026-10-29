# ADR-0026: Branding drives colours and sign-in pages; Apps Control maintains accounts through the identity provider; JSON import everywhere

**Status:** Accepted

## Context
Feedback on the internal system asked for:
- **Branding:** a theme colour, a footer image and a sign-in image, all set from the branding screen.
- **Sign-in page:** the internal system's page should follow the internal design guide.
- **User accounts:** adding users, activating and deactivating them and managing passwords, all from Apps Control,
  unless accounts come from an identity provider (Nafath, AD, LDAP, Entra ID).
- **Import:** JSON import for every screen, with a way to copy every problem found.
- **Other screens:** an icon picker, the organization structure as a tree, a redesigned Action Center, and a denser,
  steadier layout.

## Decision
- **Branding is one row and every app reads it.**
  - The entity row gains an internal-system colour, a portal colour, a footer image and a sign-in image. Images are
    branding assets, validated by content as the logo already was, up to 2 MB.
  - The public branding endpoint returns all of them. Each app applies its colour at start-up by setting its colour
    variables:
    - The internal system sets the `--dynamic-cube-gold*` family.
    - The portal sets the DGA greens and Material's primary colour.

    The shades are derived with `color-mix`. Styles read variables, never hex values, so one colour re-colours
    buttons, selection, focus rings, tabs and tints. An empty colour keeps the built-in gold or green.
- **Sign-in pages read the same branding.**
  - A script in the Keycloak themes takes the application's address from the sign-in request (`redirect_uri`) and
    reads the public branding through it. The endpoint allows any origin; it is public and read-only.
  - The script applies the entity's logo, the sign-in image and the colour.
  - The internal realm uses its own theme, `dynamic-cube-internal`, which follows the internal guide: neutral gold,
    8px controls, a 12px card and the image panel. It drops the government bar, which is for public sites. The portal
    realm keeps the DGA theme with the same branding.
- **Accounts are maintained through the identity provider, never stored by Apps Control.**
  - `IAccountDirectory` has one implementation for Keycloak's admin API. It signs in as a service account
    (client `dynamic-cube-accounts`, realm-management user rights) whose secret comes from the secret store.
  - With `AppsControl:Identity:Provider` = `None`, nothing can be changed from Apps Control.
  - `Mode`:
    - `Managed`: Apps Control creates local accounts and gives temporary passwords or emails a set-password link.
      The user must change the password at the next sign-in.
    - `Federated`: accounts come from the directory or identity provider. Creating accounts and setting passwords are
      not offered.
  - Each account's source is shown. An account linked to an LDAP/AD federation or to an identity provider keeps its
    password there, in either mode.
  - **Activating and deactivating act immediately** on both the sign-in account and the user's status in Apps
    Control. Deactivating ends the user's sessions and the next API call is refused. Being urgent, this does not wait
    for a change set.
  - Creating a user creates the sign-in account at once. The Apps Control user (names, contacts, roles) is drafted
    through the users change set as before, so an account without approved roles has no access.
- **JSON import is the package import, made easy from any screen.**
  - Every configuration table, the master screens (classes, roles, menus), the organization tree and the branding page
    accept pasted or uploaded JSON: a row, a list of rows, package items or a whole package.
  - Rows are keyed as the screen keys them. Rows that exist are updated, the others created.
  - The result goes through the existing server check and review. "Copy all problems" copies every problem, one per
    line, with item number, type, code and field.
  - Tables also export their rows as JSON in the same shape.
- **Other screen changes:**
  - **Icon picker:** an `icon` field type in the shared dynamic form opens a searchable, grouped Material Symbols
    picker; a name that is not listed can be typed. It is used for services, categories, menus and portal tiles.
  - **Organization tree:** the tree is built from the units' parent codes. Search keeps the ancestors of matches.
    Unit details show the unit's path, with actions to add a sub-unit, edit or delete.
  - **Navigation:**
    - One menu item is marked: the one whose address is the longest prefix of the current address.
    - The frame fills the window, so the menu and the page scroll separately. Opening a page never moves the menu.
    - The first entry is the **Dashboard**.
  - **Density:** compact Material density, 40px fields and table rows of about 40px, a 56px top bar and tighter
    paddings. Filter bars are a spaced white strip.
  - **Action Center:** the four boxes are count cards. Task rows open the task and show initials, relative times and
    a claim button. The task page has a meta-chip header and an action bar that stays in view.

## Consequences
- An installation sets its brand colour and images without a build. Sign-in pages follow the branding once the
  application's `/api` is reachable from the browser (it is, behind the same host as the SPA).
- A Keycloak used for account management needs the `dynamic-cube-accounts` service account and its secret in
  `Secrets:KEYCLOAK_ADMIN:clientSecret`. With an LDAP/AD federation or an identity provider, set `Mode` to `Federated`
  so creating accounts and setting passwords are not offered.
- Deactivating a user changes the user's status directly. A later change set that sets the status again wins, as for
  any field.
