/**
 * Linki trasy w Mapach Google z przystankami w ustalonej kolejności.
 *
 * PRYWATNOŚĆ: adresy to dane osobowe. Link budujemy WYŁĄCZNIE w przeglądarce i otwieramy w zewnętrznej
 * aplikacji map (target=_blank, rel=noopener noreferrer) — nie wysyłamy go do naszego API, nie logujemy
 * i nie przekazujemy do analityki. Punkt startowy = bieżąca lokalizacja w aplikacji map.
 *
 * Mapy Google przyjmują w URL maksymalnie 9 punktów pośrednich + cel, więc dłuższa trasa jest dzielona na
 * odcinki po 10 przystanków (kolejny odcinek startuje z ostatniego przystanku poprzedniego).
 */
export interface RouteLink { label: string; url: string; }

const MAX_STOPS_PER_LINK = 10;

export function routeLinks(addresses: string[]): RouteLink[] {
  const stops = addresses.map(a => (a ?? '').trim()).filter(a => a.length > 0);
  const links: RouteLink[] = [];
  let origin: string | null = null;
  for (let i = 0; i < stops.length; i += MAX_STOPS_PER_LINK) {
    const chunk = stops.slice(i, i + MAX_STOPS_PER_LINK);
    const destination = chunk[chunk.length - 1];
    const waypoints = chunk.slice(0, -1);
    const params = new URLSearchParams({ api: '1', destination, travelmode: 'driving' });
    if (origin) params.set('origin', origin);
    if (waypoints.length) params.set('waypoints', waypoints.join('|'));
    const from = i + 1, to = i + chunk.length;
    links.push({
      label: stops.length > MAX_STOPS_PER_LINK ? `Trasa: przystanki ${from}–${to}` : 'Otwórz trasę w Mapach Google',
      url: `https://www.google.com/maps/dir/?${params.toString()}`,
    });
    origin = destination;
  }
  return links;
}

/** Numer telefonu do linku tel: (bez spacji). */
export function telHref(phone: string | null | undefined): string {
  return 'tel:' + (phone ?? '').replace(/[^\d+]/g, '');
}
