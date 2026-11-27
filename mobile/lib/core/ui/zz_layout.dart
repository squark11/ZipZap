import 'package:flutter/material.dart';

import '../theme/zz_theme.dart';

/// Klasa szerokości okna wg [ZzBreakpoints].
enum ZzWidth { compact, medium, expanded, large }

extension ZzWidthX on BuildContext {
  ZzWidth get zzWidth {
    final w = MediaQuery.sizeOf(this).width;
    if (w >= ZzBreakpoints.large) return ZzWidth.large;
    if (w >= ZzBreakpoints.expanded) return ZzWidth.expanded;
    if (w >= ZzBreakpoints.medium) return ZzWidth.medium;
    return ZzWidth.compact;
  }

  /// Od 900 px: nawigacja boczna zamiast dolnej.
  bool get zzSideNav => MediaQuery.sizeOf(this).width >= ZzBreakpoints.expanded;
}

/// Wyśrodkowana kolumna treści o ograniczonej szerokości — na tablecie i komputerze listy i formularze nie
/// rozciągają się na cały ekran (design-system.md §7). Tło poza kolumną = tło ekranu.
class ZzPage extends StatelessWidget {
  final Widget child;
  final double maxWidth;
  const ZzPage({super.key, required this.child, this.maxWidth = ZzBreakpoints.singleColumn});

  @override
  Widget build(BuildContext context) => ColoredBox(
    color: context.zz.surface,
    child: Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: maxWidth),
        child: child,
      ),
    ),
  );
}
