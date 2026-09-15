import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../models/address.dart';

final addressesProvider = FutureProvider.autoDispose<List<Address>>(
    (ref) => ref.read(orderingRepositoryProvider).listAddresses());

class AddressesScreen extends ConsumerWidget {
  const AddressesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final addresses = ref.watch(addressesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Moje adresy')),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: ZzColors.orange,
        icon: const Icon(Icons.add, color: Colors.white),
        label: const Text('Dodaj adres', style: TextStyle(color: Colors.white)),
        onPressed: () => _openForm(context, ref, null),
      ),
      body: addresses.when(
        loading: () => const Center(child: CircularProgressIndicator(color: ZzColors.orange)),
        error: (e, _) => Center(child: Text(e.toString())),
        data: (list) {
          if (list.isEmpty) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(32),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.home_outlined, size: 56, color: context.zz.textMuted),
                    const SizedBox(height: 12),
                    const Text('Brak zapisanych adresów',
                        style: TextStyle(fontWeight: FontWeight.w600, fontSize: 16)),
                    const SizedBox(height: 4),
                    Text('Dodaj adres dostawy, aby szybciej składać zamówienia.',
                        textAlign: TextAlign.center, style: TextStyle(color: context.zz.textMuted)),
                  ],
                ),
              ),
            );
          }
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
            children: [for (final a in list) _AddressCard(address: a)],
          );
        },
      ),
    );
  }

  static Future<void> _openForm(BuildContext context, WidgetRef ref, Address? existing) async {
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _AddressForm(existing: existing),
    );
    if (saved == true) ref.invalidate(addressesProvider);
  }
}

class _AddressCard extends ConsumerWidget {
  final Address address;
  const _AddressCard({required this.address});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text(address.label, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(width: 8),
                if (address.isDefault)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(color: context.zz.orangeTint, borderRadius: BorderRadius.circular(6)),
                    child: const Text('Domyślny',
                        style: TextStyle(color: ZzColors.orange600, fontSize: 11, fontWeight: FontWeight.w700)),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text(address.oneLine, style: TextStyle(color: context.zz.textMuted)),
            if (address.notes != null && address.notes!.isNotEmpty) ...[
              const SizedBox(height: 2),
              Text('Uwagi: ${address.notes}', style: TextStyle(color: context.zz.textMuted, fontSize: 12)),
            ],
            const SizedBox(height: 6),
            Row(
              children: [
                if (!address.isDefault)
                  TextButton(
                    onPressed: () async {
                      await ref.read(orderingRepositoryProvider).setDefaultAddress(address.id);
                      ref.invalidate(addressesProvider);
                    },
                    child: const Text('Ustaw domyślny'),
                  ),
                TextButton(
                  onPressed: () => AddressesScreen._openForm(context, ref, address),
                  child: const Text('Edytuj'),
                ),
                TextButton(
                  style: TextButton.styleFrom(foregroundColor: ZzColors.danger),
                  onPressed: () async {
                    await ref.read(orderingRepositoryProvider).deleteAddress(address.id);
                    ref.invalidate(addressesProvider);
                  },
                  child: const Text('Usuń'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AddressForm extends ConsumerStatefulWidget {
  final Address? existing;
  const _AddressForm({required this.existing});

  @override
  ConsumerState<_AddressForm> createState() => _AddressFormState();
}

class _AddressFormState extends ConsumerState<_AddressForm> {
  late final TextEditingController _label;
  late final TextEditingController _street;
  late final TextEditingController _building;
  late final TextEditingController _apartment;
  late final TextEditingController _postal;
  late final TextEditingController _city;
  late final TextEditingController _notes;
  late bool _default;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    final a = widget.existing;
    _label = TextEditingController(text: a?.label ?? 'Dom');
    _street = TextEditingController(text: a?.street ?? '');
    _building = TextEditingController(text: a?.buildingNo ?? '');
    _apartment = TextEditingController(text: a?.apartmentNo ?? '');
    _postal = TextEditingController(text: a?.postalCode ?? '');
    _city = TextEditingController(text: a?.city ?? '');
    _notes = TextEditingController(text: a?.notes ?? '');
    _default = a?.isDefault ?? false;
  }

  @override
  void dispose() {
    for (final c in [_label, _street, _building, _apartment, _postal, _city, _notes]) {
      c.dispose();
    }
    super.dispose();
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  Future<void> _save() async {
    final postalDigits = _postal.text.replaceAll(RegExp(r'\D'), '');
    if (_street.text.trim().isEmpty || _building.text.trim().isEmpty || _city.text.trim().isEmpty) {
      _snack('Podaj ulicę, numer domu i miasto.');
      return;
    }
    if (postalDigits.length != 5) {
      _snack('Podaj poprawny kod pocztowy (XX-XXX).');
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(orderingRepositoryProvider).saveAddress({
        'label': _label.text.trim(),
        'street': _street.text.trim(),
        'buildingNo': _building.text.trim(),
        'apartmentNo': _apartment.text.trim().isEmpty ? null : _apartment.text.trim(),
        'postalCode': _postal.text.trim(),
        'city': _city.text.trim(),
        'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
        'isDefault': _default,
      }, id: widget.existing?.id);
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      _snack(e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 20, 20, 20 + bottom),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(widget.existing == null ? 'Nowy adres' : 'Edytuj adres',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 16),
            TextField(controller: _label, decoration: const InputDecoration(labelText: 'Etykieta (np. Dom)')),
            const SizedBox(height: 12),
            TextField(controller: _street, decoration: const InputDecoration(labelText: 'Ulica')),
            const SizedBox(height: 12),
            Row(children: [
              Expanded(child: TextField(controller: _building, decoration: const InputDecoration(labelText: 'Nr domu'))),
              const SizedBox(width: 12),
              Expanded(child: TextField(controller: _apartment, decoration: const InputDecoration(labelText: 'Nr lokalu (opc.)'))),
            ]),
            const SizedBox(height: 12),
            Row(children: [
              SizedBox(
                width: 130,
                child: TextField(
                  controller: _postal,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Kod pocztowy'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(child: TextField(controller: _city, decoration: const InputDecoration(labelText: 'Miasto'))),
            ]),
            const SizedBox(height: 12),
            TextField(controller: _notes, decoration: const InputDecoration(labelText: 'Uwagi dla kuriera (opc.)')),
            const SizedBox(height: 8),
            CheckboxListTile(
              value: _default,
              onChanged: (v) => setState(() => _default = v ?? false),
              controlAffinity: ListTileControlAffinity.leading,
              contentPadding: EdgeInsets.zero,
              activeColor: ZzColors.orange,
              title: const Text('Ustaw jako domyślny'),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: _busy ? null : _save,
                child: _busy
                    ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Text('Zapisz adres'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
