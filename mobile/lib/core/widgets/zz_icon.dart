import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../theme/zz_theme.dart';

/// Ikona z zestawu marki (`assets/svg/icons/<name>.svg`).
///
/// Ikony są jednokolorowe (line, 2px). Barwę bierzemy z ambientowego
/// [IconTheme] (dzięki temu w AppBar / NavigationBar tintują się jak zwykłe
/// [Icon]) — chyba że podano jawnie [color]. Jednolity kolor nakładamy przez
/// [ColorFilter] (`srcIn`), więc źródłowy kolor w pliku SVG nie ma znaczenia.
class ZzIcon extends StatelessWidget {
  final String name;
  final double size;
  final Color? color;

  const ZzIcon(this.name, {super.key, this.size = 24, this.color});

  @override
  Widget build(BuildContext context) {
    final c = color ?? IconTheme.of(context).color ?? ZzColors.graphite;
    return SvgPicture.asset(
      'assets/svg/icons/$name.svg',
      width: size,
      height: size,
      colorFilter: ColorFilter.mode(c, BlendMode.srcIn),
    );
  }
}
