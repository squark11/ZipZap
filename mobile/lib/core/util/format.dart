import 'package:intl/intl.dart';

final NumberFormat _zl =
    NumberFormat.currency(locale: 'pl_PL', symbol: 'zł', decimalDigits: 2);

/// Formatuje kwotę w złotych, np. `23,50 zł`.
String zl(num value) => _zl.format(value);

final DateFormat _dt = DateFormat('dd.MM HH:mm', 'pl_PL');

/// Krótki znacznik czasu, np. `09.09 14:30`.
String shortDateTime(DateTime utc) => _dt.format(utc.toLocal());

final DateFormat _t = DateFormat('HH:mm', 'pl_PL');

/// Sama godzina, np. `14:30`.
String shortTime(DateTime utc) => _t.format(utc.toLocal());

/// Przyjazna data z ISO `yyyy-MM-dd`: „Dziś" / „Jutro" / `dd.MM`.
String friendlyDate(String iso) {
  final d = DateTime.tryParse(iso);
  if (d == null) return iso;
  final now = DateTime.now();
  final today = DateTime(now.year, now.month, now.day);
  final that = DateTime(d.year, d.month, d.day);
  final diff = that.difference(today).inDays;
  if (diff == 0) return 'Dziś';
  if (diff == 1) return 'Jutro';
  return '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}';
}
