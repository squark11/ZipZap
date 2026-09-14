import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';
import 'zz_icon.dart';

/// Logo sklepu (URL) z fallbackiem do ikony marki, gdy brak logo lub URL się nie wczyta.
class StoreLogo extends StatelessWidget {
  final String? logoUrl;
  final double size;
  const StoreLogo({super.key, required this.logoUrl, this.size = 48});

  @override
  Widget build(BuildContext context) {
    final fallback = Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: context.zz.orangeTint,
        borderRadius: BorderRadius.circular(ZzRadius.md),
      ),
      alignment: Alignment.center,
      child: ZzIcon('store', size: size * 0.5, color: ZzColors.orange),
    );
    if (logoUrl == null) return fallback;
    return ClipRRect(
      borderRadius: BorderRadius.circular(ZzRadius.md),
      child: Image.network(
        logoUrl!,
        width: size,
        height: size,
        fit: BoxFit.cover,
        errorBuilder: (_, _, _) => fallback,
        loadingBuilder: (context, child, progress) => progress == null ? child : fallback,
      ),
    );
  }
}
