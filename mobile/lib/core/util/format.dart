import 'package:intl/intl.dart';

final NumberFormat _zl =
    NumberFormat.currency(locale: 'pl_PL', symbol: 'zł', decimalDigits: 2);

/// Formatuje kwotę w złotych, np. `23,50 zł`.
String zl(num value) => _zl.format(value);

final DateFormat _dt = DateFormat('dd.MM HH:mm', 'pl_PL');

/// Krótki znacznik czasu, np. `09.09 14:30`.
String shortDateTime(DateTime utc) => _dt.format(utc.toLocal());
