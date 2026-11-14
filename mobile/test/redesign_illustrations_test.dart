import 'dart:io';

import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

/// Redesign, Etap 1 (E1-4): każda nowa ilustracja i ikona kategorii musi dać się sparsować przez flutter_svg
/// (bez nieobsługiwanych filtrów/stylów), mieć opis dla czytnika ekranu i mieścić się w budżecie wagi.
/// Pliki nie są jeszcze w `pubspec.yaml` (dołączy je Etap 2), więc test czyta je z dysku.
void main() {
  final dirs = {
    'assets/svg/illustrations': 12 * 1024,
    'assets/svg/categories': 2 * 1024,
  };

  for (final entry in dirs.entries) {
    final files = Directory(entry.key).listSync().whereType<File>().where((f) => f.path.endsWith('.svg')).toList()
      ..sort((a, b) => a.path.compareTo(b.path));

    test('${entry.key}: pliki istnieją', () => expect(files, isNotEmpty));

    for (final file in files) {
      final name = file.uri.pathSegments.last;
      testWidgets('${entry.key}/$name parsuje się, ma <title> i ≤ ${entry.value ~/ 1024} KB', (tester) async {
        final source = file.readAsStringSync();
        expect(source.length, lessThanOrEqualTo(entry.value), reason: 'za duży plik');
        expect(source, contains('<title>'), reason: 'brak opisu dla czytnika ekranu');
        expect(source, isNot(contains('<image')), reason: 'osadzona bitmapa');
        expect(source, isNot(contains('<style')), reason: 'style CSS nieobsługiwane przez flutter_svg');
        expect(source, isNot(contains('filter')), reason: 'filtry nieobsługiwane przez flutter_svg');
        await tester.runAsync(() async {
          final info = await vg.loadPicture(SvgStringLoader(source), null);
          expect(info.size.width, greaterThan(0));
          info.picture.dispose();
        });
      });
    }
  }
}
