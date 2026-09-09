---
name: zipzap-app-design
description: >-
  Design language and UI/UX patterns for the ZipZap consumer app (Flutter) and
  admin panel (Angular), modeled on modern food/grocery delivery apps (Wolt,
  Glovo, Bolt Food, Pyszne.pl, Uber Eats, Deliveroo). Use whenever building,
  refactoring, styling, or reviewing ANY app screen, widget/component, layout,
  navigation, list/card, empty/error/loading state, illustration or icon.
---

# ZipZap — App Design System

Build ZipZap so it feels like a top-tier delivery app (Wolt/Glovo/Bolt Food/Pyszne),
but unmistakably ZipZap. **One accent color (orange), image-forward, rounded cards,
generous whitespace, fast.** When in doubt, copy the *structure* competitors use and
apply ZipZap's brand.

## 0. Non‑negotiables
- Brand tokens only — never hardcode hex/spacing. Source of truth: `/branding`
  (orange `#F97316`, green `#22C55E` success, graphite `#3A3F4B` text). Fonts:
  **Poppins** headings, **Inter** UI/body.
- Delivery fee is ALWAYS shown as a **separate line** from cart value (business rule).
- **Show the store's delivery method + schedule** on the store card and store page (and
  reflect it in checkout slots): ZipZap runs **scheduled waves** (e.g. „Dostawa ZipZap ·
  dziś 16:00"), a merchant-delivery store shows its own („Dostawa sklepu · na bieżąco" or
  a time). We are NOT on-demand like Glovo — the customer must know *when* they'll receive it.
- Every screen ships its four states: **loading (skeleton) · empty · error · offline**.
- Touch targets ≥ 48px; contrast ≥ 4.5:1; support light **and** dark.
- Money `pl_PL` (`23,50 zł`); one primary CTA per screen.

## 1. Design DNA (distilled from competitors)
- **Single accent per brand** (Glovo=yellow, Deliveroo=teal, Wolt=blue → ZipZap=orange).
  Use orange sparingly: primary CTA, active state, price/badge accents. Everything
  else is graphite/neutral on white.
- **Round category chips** in a horizontal scroller at the top of Home (Wolt): icon in a
  soft circle + short label (Sklepy, Restauracje, Pieczywo, Owoce, Napoje, Apteka…).
- **Image-forward cards** for stores/products (photo + name + rating ★ + delivery time +
  fee + min-order). Horizontal carousels with section headers ("Zamów ponownie",
  "W pobliżu", "Promocje").
- **Hero/promo banner** with a photo + graphite overlay + white heading + CTA.
- **Floating search** pill / prominent search on Home (Wolt/Glovo).
- **Store/menu page**: hero image, delivery meta chips (czas · opłata · min.), sticky
  category tabs, product rows with thumbnail + price + add button.
- **Bottom navigation** (app): Sklepy · Szukaj · Koszyk · Zamówienia · Konto.
- Rounded corners (`ZzRadius.lg` = 16 for cards, `md` = 10 for controls), soft shadows,
  lots of whitespace, bold short headings.

## 2. Layout & spacing
- 4‑pt spacing scale (4/8/12/16/24/32). Screen padding 16. Card padding 12–16.
- Max content width on web/tablet ~640; center it. Respect safe areas.
- Vertical rhythm: section header (titleMedium) → 8 → content → 24 → next section.

## 3. Core screen patterns (consumer app)
| Screen | Pattern |
|---|---|
| **Splash** | Wordmark + spinner; bootstrap session, then Home. |
| **Onboarding** | 3 slides (wartość → zaufanie/śledzenie → lokalizacja), each: SVG illustration + heading + one line + dots; skip + „Dalej". |
| **Home** | Search on top → round category chips → promo hero → carousels (Zamów ponownie / W pobliżu / Promocje) with store cards. |
| **Store list** | Filter/sort bar → vertical list of store cards (photo, name, ★, czas, opłata, status pill). |
| **Store / Menu** | Hero image + meta chips + sticky category tabs → product rows (thumb, name, price, `+`). Restaurant = menu with modifiers. |
| **Product / item** | Image, description, options/dodatki (radio/checkbox), qty stepper, „Dodaj do koszyka" sticky bottom. |
| **Cart** | Line items with steppers, subtotal, **delivery fee separate**, min‑order note, CTA. |
| **Checkout** | Address, slot/zone, payment method (BLIK/karta), summary (subtotal + fee = total), CTA. |
| **Payment** | Open provider redirect; **poll status (webhook‑authoritative)**; never mark success client‑side. |
| **Confirmation** | Success SVG + order number + „Śledź zamówienie". |
| **Tracking** | Status timeline (Złożone→…→Dostarczone), later map + courier marker; ETA. |
| **Account** | Orders, addresses, notifications, payments, help. |

## 4. Components (spec)
- **Buttons**: primary = orange fill, white text, 50px, radius md; secondary = outline;
  ghost = text; danger = red. One primary per view.
- **Card**: white, 1px border `--zz-border`, radius lg, shadow-sm; tappable via InkWell.
- **Category chip**: circle (56px) soft-orange bg + icon, label below (caption).
- **Store card**: 16:9 photo (top), body: name (titleMedium), ★ rating · czas · opłata
  (muted caption), status pill top-right.
- **Product row**: 46–56px thumb, name + `cena / jednostka`, `Dodaj`/stepper on right.
- **Qty stepper**: bordered pill `– n +`, orange glyphs.
- **Status pill**: colored by status (see `orders`/tracking mapping); rounded sm.
- **Bottom bar / cart bar**: orange full-width CTA showing count + subtotal + „→".
- **App bar**: white, graphite title (Poppins 700), actions (bell w/ unread, cart, account).

## 5. Imagery & illustrations
- **Photos** (food/grocery): consistent warm crop; graphite overlay behind white text;
  WebP/AVIF + responsive sizes; blur placeholder; lazy‑load. Check licenses before publish.
- **Illustrations = SVG**, ZipZap palette, flat/isometric, theme‑aware (`currentColor`
  where possible). Used for onboarding, empty/error/success, section heroes. Provided
  `.ai/.eps` packs must be converted to SVG (Inkscape/Illustrator) then SVGO‑optimized.
- **Icons**: one consistent line set (~2px stroke, `currentColor`). Flutter: `flutter_svg`.
- Every meaningful SVG: `<title>`; decorative ones `aria-hidden`/excludeSemantics.

## 6. Motion & micro‑interactions
- Add‑to‑cart: badge bump + subtle scale. Status change: cross‑fade. Pull‑to‑refresh.
- Page transitions: platform default + shared‑axis for onboarding. Keep < 250ms, ease‑out.
- Skeleton shimmer for loading; never a bare spinner on a full screen if a skeleton fits.

## 7. States (write all four, every screen)
- **Loading**: skeletons matching the layout.
- **Empty**: SVG + one‑line reason + primary CTA (e.g., pusty koszyk → „Przeglądaj sklepy").
- **Error**: SVG + human message + „Spróbuj ponownie".
- **Offline**: top banner; queue writes; show last data.

## 8. Accessibility & i18n
- Semantics/labels on interactive widgets; logical focus order; dynamic text scale.
- PL default + EN ready; never concatenate translated fragments.

## 9. Do / Don't
- ✅ Copy competitor *structure*; ✅ one accent; ✅ photos + SVG; ✅ separate delivery fee;
  ✅ tokens; ✅ four states; ✅ dark mode.
- ❌ Rainbow of colors; ❌ raster icons; ❌ hardcoded hex/px; ❌ full‑screen spinners;
  ❌ client‑side payment success; ❌ dense text walls; ❌ tiny tap targets.

## 10. Implementation notes
- Flutter: extend `ZzTheme` (add dark), keep `ZzColors/ZzRadius` tokens; `flutter_svg`
  for assets under `mobile/assets/svg`; `google_fonts` Poppins/Inter (or bundle).
- Angular panel: reuse the same tokens/inline‑SVG icon style already in the rail.
- Reference `ADVANCED_APP_ROADMAP.md` (§4–5) for the design‑system + SVG production plan.
