import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';

/// Pilotaż W1: jasny komunikat „zamówienie testowe — bez opłaty".
/// Zastępuje ekran płatności — klient nie widzi „Do zapłaty" ani oczekiwania na bramkę.
class TestOrderBanner extends StatelessWidget {
  /// Użytkownik bez zaproszenia do testów — nie może złożyć zamówienia.
  final bool blocked;

  /// Wariant na śledzeniu już złożonego zamówienia.
  final bool placed;

  const TestOrderBanner({super.key, this.blocked = false, this.placed = false});

  @override
  Widget build(BuildContext context) {
    final String title;
    final String body;
    if (blocked) {
      title = 'Pilotaż dla zaproszonych testerów';
      body = 'Możesz przeglądać ofertę, ale zamówienia składają na razie tylko osoby zaproszone do testów.';
    } else if (placed) {
      title = 'Zamówienie testowe — bez opłaty';
      body = 'Nie pobieramy żadnej płatności. Zakupy robimy w najbliższej rundzie, a dostawę widzisz poniżej.';
    } else {
      title = 'Zamówienie testowe — bez opłaty';
      body = 'To pilotaż dla zaproszonych testerów: nie pobieramy płatności. Towar kupi i dowiezie Dowózka.pl.';
    }
    return Semantics(
      container: true,
      label: '$title. $body',
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: context.zz.orangeTint,
          border: Border.all(color: ZzColors.orange),
          borderRadius: BorderRadius.circular(ZzRadius.md),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(blocked ? Icons.lock_outline : Icons.science_outlined,
                color: ZzColors.orange600, size: 22),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title,
                      style: const TextStyle(
                          fontWeight: FontWeight.w700, color: ZzColors.orange600)),
                  const SizedBox(height: 4),
                  Text(body, style: TextStyle(color: context.zz.text, fontSize: 13)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
