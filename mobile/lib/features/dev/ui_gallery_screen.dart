import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../../core/theme/zz_theme.dart';
import '../../core/ui/ui.dart';
import '../../core/widgets/skeleton.dart';
import '../../core/widgets/states.dart';
import '../../models/store.dart';

/// Galeria komponentów Design Systemu (E2-3) — trasa `/dev/ui` istnieje tylko w trybie deweloperskim.
/// Służy do przeglądu wyglądu i stanów w trybie jasnym, ciemnym i przy powiększonym tekście.
class UiGalleryScreen extends StatefulWidget {
  const UiGalleryScreen({super.key});

  @override
  State<UiGalleryScreen> createState() => _UiGalleryScreenState();
}

class _UiGalleryScreenState extends State<UiGalleryScreen> {
  int _qty = 2;
  String _category = 'owoce';

  static final _store = Store.fromJson({
    'id': '00000000-0000-0000-0000-000000000001',
    'name': 'Sklep przykładowy',
    'slug': 'przyklad',
    'city': 'Rabka-Zdrój',
    'status': 'Open',
    'minimumOrderValue': 20,
    'isAcceptingOrders': true,
  });

  static const _categories = {
    'wszystko': 'Wszystko',
    'owoce': 'Owoce',
    'warzywa': 'Warzywa',
    'pieczywo': 'Pieczywo',
    'nabial': 'Nabiał',
    'mieso': 'Mięso',
  };

  @override
  Widget build(BuildContext context) {
    Widget section(String title, List<Widget> children) => Padding(
          padding: const EdgeInsets.only(bottom: ZzSpace.xxl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [ZzSectionHeader(title: title), const SizedBox(height: ZzSpace.sm), ...children],
          ),
        );
    return Scaffold(
      appBar: AppBar(title: const Text('Komponenty')),
      body: ZzPage(
        maxWidth: ZzBreakpoints.singleColumn,
        child: ListView(
          padding: const EdgeInsets.all(ZzSpace.screen),
          children: [
            section('Przyciski', [
              ElevatedButton(onPressed: () {}, child: const Text('Przejdź do zamówienia')),
              const SizedBox(height: ZzSpace.sm),
              OutlinedButton(onPressed: () {}, child: const Text('Kontynuuj z Google')),
              const SizedBox(height: ZzSpace.sm),
              const ElevatedButton(onPressed: null, child: Text('Nieaktywny')),
              TextButton(onPressed: () {}, child: const Text('Nie pamiętasz hasła?')),
            ]),
            section('Pola', [
              const ZzSearchField(hint: 'Szukaj: mleko, chleb, Lewiatan…'),
              const SizedBox(height: ZzSpace.md),
              const TextField(decoration: InputDecoration(labelText: 'E-mail')),
              const SizedBox(height: ZzSpace.md),
              const TextField(decoration: InputDecoration(labelText: 'Kod pocztowy', errorText: 'Kod ma 5 cyfr, np. 31-042.')),
            ]),
            section('Kategorie', [
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(children: [
                  for (final e in _categories.entries)
                    ZzCategoryChip(
                      label: e.value,
                      icon: e.key,
                      selected: _category == e.key,
                      onTap: () => setState(() => _category = e.key),
                    ),
                ]),
              ),
            ]),
            section('Karta sklepu i licznik', [
              ZzStoreCard(store: _store, onTap: () {}),
              const SizedBox(height: ZzSpace.md),
              Row(children: [
                ZzQtyStepper(
                  itemLabel: 'Jabłka',
                  quantity: _qty,
                  valueText: '$_qty kg',
                  onMinus: _qty > 1 ? () => setState(() => _qty--) : null,
                  onPlus: () => setState(() => _qty++),
                ),
                const SizedBox(width: ZzSpace.md),
                const ZzStatusChip(open: false),
              ]),
            ]),
            section('Komunikaty', const [
              ZzNotice(kind: ZzNoticeKind.info, message: 'Zamów do 15:30 — dostawa dziś od 17:00.'),
              SizedBox(height: ZzSpace.sm),
              ZzNotice(kind: ZzNoticeKind.success, message: 'Ten sklep dowozi pod Twój adres.'),
              SizedBox(height: ZzSpace.sm),
              ZzNotice(kind: ZzNoticeKind.warning, title: 'Pilotaż', message: 'Zamówienie testowe — bez płatności.'),
              SizedBox(height: ZzSpace.sm),
              ZzNotice(kind: ZzNoticeKind.danger, message: 'Ten termin właśnie się zapełnił. Wybierz inny.'),
            ]),
            section('Statusy zamówienia', [
              const Wrap(spacing: ZzSpace.sm, runSpacing: ZzSpace.sm, children: [
                StatusPill('Placed'),
                StatusPill('Picking'),
                StatusPill('InDelivery'),
                StatusPill('Delivered'),
                StatusPill('Cancelled'),
              ]),
            ]),
            section('Ładowanie', const [ZzSkeleton(height: 18), SizedBox(height: ZzSpace.sm), ZzSkeleton(width: 200)]),
            section('Ilustracje', [
              Wrap(spacing: ZzSpace.md, runSpacing: ZzSpace.md, children: [
                for (final n in const ['mascot', 'state_empty_cart', 'state_error', 'state_offline', 'state_success'])
                  SvgPicture.asset('assets/svg/illustrations/$n.svg', height: 96),
              ]),
            ]),
            ZzCartBar(count: 3, subtotal: 22.46, missingToMinimum: 0, onTap: () {}),
          ],
        ),
      ),
    );
  }
}
