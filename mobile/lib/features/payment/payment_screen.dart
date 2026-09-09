import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/api/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/zz_theme.dart';
import '../../core/util/format.dart';
import '../../core/widgets/states.dart';
import '../../models/payment.dart';

class PaymentScreen extends ConsumerStatefulWidget {
  final String orderId;
  const PaymentScreen({super.key, required this.orderId});

  @override
  ConsumerState<PaymentScreen> createState() => _PaymentScreenState();
}

class _PaymentScreenState extends ConsumerState<PaymentScreen> {
  PaymentInfo? _payment;
  String? _error;
  bool _loading = true;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) => _poll());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final p = await ref.read(paymentsRepositoryProvider).getMyPayment(widget.orderId);
      if (!mounted) return;
      setState(() {
        _payment = p;
        _loading = false;
        _error = null;
      });
      if (!p.isPending) _timer?.cancel();
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  Future<void> _poll() async {
    if (_payment != null && !_payment!.isPending) {
      _timer?.cancel();
      return;
    }
    await _load();
  }

  Future<void> _pay() async {
    final url = _payment?.redirectUrl;
    if (url == null) return;
    final uri = Uri.parse(url);
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!ok && mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Nie udało się otworzyć strony płatności.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Płatność'), automaticallyImplyLeading: false),
      body: _loading
          ? const LoadingView(label: 'Przygotowuję płatność…')
          : _error != null
              ? ErrorView(message: _error!, onRetry: _load)
              : _body(),
    );
  }

  Widget _body() {
    final p = _payment!;
    if (p.isAuthorized) return _resolved(success: true);
    if (p.isFailed) return _resolved(success: false);
    return _pending(p);
  }

  Widget _pending(PaymentInfo p) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 8),
          Text('Do zapłaty', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(zl(p.amount),
              style: Theme.of(context)
                  .textTheme
                  .displaySmall
                  ?.copyWith(color: ZzColors.orange)),
          const SizedBox(height: 6),
          Text('w tym dostawa ${zl(p.deliveryFee)}',
              style: const TextStyle(color: ZzColors.textMuted)),
          const SizedBox(height: 28),
          ElevatedButton.icon(
            onPressed: p.redirectUrl == null ? null : _pay,
            icon: const Icon(Icons.lock_outline),
            label: const Text('Zapłać teraz'),
          ),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: ZzColors.surface,
              borderRadius: BorderRadius.circular(ZzRadius.md),
            ),
            child: const Row(
              children: [
                SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2, color: ZzColors.orange),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'Czekam na potwierdzenie płatności od dostawcy. '
                    'Status zaktualizuje się tu automatycznie.',
                    style: TextStyle(color: ZzColors.textMuted, fontSize: 13),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _resolved({required bool success}) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(success ? Icons.check_circle : Icons.cancel,
              size: 72, color: success ? ZzColors.green : ZzColors.danger),
          const SizedBox(height: 16),
          Text(success ? 'Płatność potwierdzona' : 'Płatność nieudana',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 8),
          Text(
            success
                ? 'Sklep przygotuje Twoje zamówienie. Możesz śledzić jego status.'
                : 'Płatność nie została zrealizowana. Spróbuj ponownie.',
            textAlign: TextAlign.center,
            style: const TextStyle(color: ZzColors.textMuted),
          ),
          const SizedBox(height: 28),
          if (success)
            ElevatedButton(
              onPressed: () => context.go('/orders/${widget.orderId}'),
              child: const Text('Śledź zamówienie'),
            )
          else ...[
            ElevatedButton(onPressed: _pay, child: const Text('Spróbuj ponownie')),
            const SizedBox(height: 10),
            OutlinedButton(
              onPressed: () => context.go('/stores'),
              child: const Text('Wróć do sklepów'),
            ),
          ],
        ],
      ),
    );
  }
}
