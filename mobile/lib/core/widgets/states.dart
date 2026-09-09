import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../theme/zz_theme.dart';

class LoadingView extends StatelessWidget {
  final String? label;
  const LoadingView({super.key, this.label});

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(color: ZzColors.orange),
            if (label != null) ...[
              const SizedBox(height: 12),
              Text(label!, style: const TextStyle(color: ZzColors.textMuted)),
            ],
          ],
        ),
      );
}

class ErrorView extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;
  const ErrorView({super.key, required this.message, this.onRetry});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 44, color: ZzColors.danger),
              const SizedBox(height: 12),
              Text(message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: ZzColors.text, fontSize: 16)),
              if (onRetry != null) ...[
                const SizedBox(height: 16),
                OutlinedButton.icon(
                  onPressed: onRetry,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Spróbuj ponownie'),
                ),
              ],
            ],
          ),
        ),
      );
}

class EmptyView extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? subtitle;

  /// Opcjonalna ilustracja SVG (np. `assets/svg/empty_cart.svg`); gdy null — ikona.
  final String? svgAsset;

  const EmptyView({
    super.key,
    required this.icon,
    required this.title,
    this.subtitle,
    this.svgAsset,
  });

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (svgAsset != null)
                SvgPicture.asset(svgAsset!, height: 132)
              else
                Icon(icon, size: 48, color: ZzColors.textMuted),
              const SizedBox(height: 12),
              Text(title,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
              if (subtitle != null) ...[
                const SizedBox(height: 6),
                Text(subtitle!,
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: ZzColors.textMuted)),
              ],
            ],
          ),
        ),
      );
}

/// Kolorowa „pigułka" statusu zamówienia.
class StatusPill extends StatelessWidget {
  final String status;
  const StatusPill(this.status, {super.key});

  @override
  Widget build(BuildContext context) {
    final (bg, fg, label) = _style(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(ZzRadius.sm)),
      child: Text(label,
          style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }

  static (Color, Color, String) _style(String s) {
    switch (s) {
      case 'Placed':
        return (ZzColors.orange50, ZzColors.orange600, 'Złożone');
      case 'Confirmed':
        return (ZzColors.orange50, ZzColors.orange600, 'Potwierdzone');
      case 'Picking':
        return (const Color(0xFFEFF6FF), const Color(0xFF2563EB), 'Kompletowane');
      case 'ReadyForPickup':
        return (const Color(0xFFEFF6FF), const Color(0xFF2563EB), 'Gotowe do odbioru');
      case 'InDelivery':
        return (ZzColors.orange50, ZzColors.orange600, 'W dostawie');
      case 'Delivered':
        return (ZzColors.green50, const Color(0xFF128040), 'Dostarczone');
      case 'Completed':
        return (ZzColors.green50, const Color(0xFF128040), 'Zakończone');
      case 'Cancelled':
        return (const Color(0xFFFEECEC), ZzColors.danger, 'Anulowane');
      default:
        return (ZzColors.surface, ZzColors.textMuted, s);
    }
  }
}
