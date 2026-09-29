/// Cel przekierowania po logowaniu — WYŁĄCZNIE ścieżka wewnątrz aplikacji („/s/…", „/checkout").
/// Pełny adres, „//host", ukośnik wsteczny, `/login` i `/splash` są odrzucane (brak otwartego przekierowania
/// z linku `?redirect=` podsuniętego przez kogoś innego).
String? safeInternalRedirect(String? raw) {
  if (raw == null) return null;
  final v = raw.trim();
  if (v.isEmpty || !v.startsWith('/') || v.startsWith('//') || v.contains(r'\')) return null;
  final uri = Uri.tryParse(v);
  if (uri == null || uri.hasScheme || uri.hasAuthority) return null;
  if (uri.path == '/login' || uri.path.startsWith('/splash')) return null;
  return v;
}

/// Ścieżka stałego linku do oferty sklepu (ten sam format co w kodzie QR, bez parametru źródła).
String storePath(String slugOrId) => '/s/${Uri.encodeComponent(slugOrId)}';
