/// Tekst do wyszukiwania: małe litery, bez polskich znaków i nadmiarowych spacji („Żółty ser" → „zolty ser").
String normalizeSearch(String s) {
  const from = 'ąćęłńóśźż', to = 'acelnoszz';
  final b = StringBuffer();
  for (final ch in s.toLowerCase().split('')) {
    final i = from.indexOf(ch);
    b.write(i >= 0 ? to[i] : ch);
  }
  return b.toString().trim().replaceAll(RegExp(r'\s+'), ' ');
}

/// Czy [text] zawiera wszystkie słowa zapytania (kolejność dowolna: „ser zolty" znajdzie „Żółty ser").
bool matchesSearch(String text, String query) {
  final q = normalizeSearch(query);
  if (q.isEmpty) return true;
  final t = normalizeSearch(text);
  return q.split(' ').every(t.contains);
}
