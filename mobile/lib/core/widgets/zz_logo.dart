import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../theme/zz_theme.dart';

/// Pozioma sygnatura marki: znak (wózek + strzałka) + wordmark „Dowózka.pl”.
///
/// W aplikacji komponujemy znak (SVG) z tekstem w Poppins — dzięki temu
/// krój jest spójny z resztą UI (bez zależności od fontu wewnątrz SVG).
/// `mono: true` używa monochromatycznego znaku (na kolorowym tle).
class ZzLogo extends StatelessWidget {
  final double height;
  final bool mono;
  final Color? wordmarkColor;

  const ZzLogo({
    super.key,
    this.height = 40,
    this.mono = false,
    this.wordmarkColor,
  });

  @override
  Widget build(BuildContext context) {
    final base = Theme.of(context).textTheme.displaySmall?.copyWith(
      color: wordmarkColor ?? context.zz.heading,
      fontSize: height * 0.72,
    );
    // FittedBox: na wąskim ekranie albo przy powiększonym tekście systemowym logo się zmniejsza, zamiast wychodzić
    // poza ekran (wykryte testem układu 360 px).
    return FittedBox(
      fit: BoxFit.scaleDown,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SvgPicture.asset(
            mono ? 'assets/svg/logo_mono.svg' : 'assets/svg/logo_mark.svg',
            height: height,
          ),
          SizedBox(width: height * 0.22),
          RichText(
            text: TextSpan(
              style: base,
              children: [
                const TextSpan(text: 'Dowózka'),
                TextSpan(
                  text: '.pl',
                  style: TextStyle(
                    color: mono
                        ? (wordmarkColor ?? Colors.white)
                        : context.zz.brandStrong,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
