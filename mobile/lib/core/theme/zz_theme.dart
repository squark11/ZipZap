import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Akcenty marki ZipZap — stałe niezależne od trybu (jasny/ciemny).
class ZzColors {
  static const orange = Color(0xFFF97316); // primary
  static const orange600 = Color(0xFFEA6A0C);
  static const orange50 = Color(0xFFFFF3EA);
  static const green = Color(0xFF22C55E); // secondary / sukces
  static const green50 = Color(0xFFE9FBF0);
  static const danger = Color(0xFFEF4444);
  static const warning = Color(0xFFF59E0B);

  // Neutrale (wartości trybu jasnego) — do miejsc bez kontekstu / fallbacków.
  // W widgetach używaj `context.zz.<token>`, aby adaptowały się do trybu.
  static const graphite = Color(0xFF3A3F4B); // nagłówki / wordmark
  static const bg = Color(0xFFFFFFFF);
  static const surface = Color(0xFFF7F8FA);
  static const border = Color(0xFFE5E7EB);
  static const text = Color(0xFF3A3F4B);
  static const textMuted = Color(0xFF6B7280);
}

class ZzRadius {
  static const sm = 6.0;
  static const md = 10.0;
  static const lg = 16.0;
}

/// Neutrale i tła zależne od trybu. Pobieraj przez `context.zz`.
@immutable
class ZzPalette extends ThemeExtension<ZzPalette> {
  final Color bg; // karty, appbar, nawigacja
  final Color surface; // tło ekranu, pola, chipy
  final Color border;
  final Color text; // treść
  final Color textMuted; // podpisy
  final Color heading; // nagłówki / wordmark (graphite)
  final Color orangeTint; // tło akcentu pomarańczowego (orange50)
  final Color greenTint; // tło akcentu zielonego (green50)

  const ZzPalette({
    required this.bg,
    required this.surface,
    required this.border,
    required this.text,
    required this.textMuted,
    required this.heading,
    required this.orangeTint,
    required this.greenTint,
  });

  static const light = ZzPalette(
    bg: Color(0xFFFFFFFF),
    surface: Color(0xFFF7F8FA),
    border: Color(0xFFE5E7EB),
    text: Color(0xFF3A3F4B),
    textMuted: Color(0xFF6B7280),
    heading: Color(0xFF3A3F4B),
    orangeTint: Color(0xFFFFF3EA),
    greenTint: Color(0xFFE9FBF0),
  );

  static const dark = ZzPalette(
    bg: Color(0xFF1C1E23), // karty (nieco jaśniejsze od tła)
    surface: Color(0xFF121316), // tło ekranu (najciemniejsze)
    border: Color(0xFF2C2F36),
    text: Color(0xFFE6E8ED),
    textMuted: Color(0xFF9AA0AB),
    heading: Color(0xFFF2F3F6),
    orangeTint: Color(0xFF37291A),
    greenTint: Color(0xFF17301F),
  );

  @override
  ZzPalette copyWith({
    Color? bg,
    Color? surface,
    Color? border,
    Color? text,
    Color? textMuted,
    Color? heading,
    Color? orangeTint,
    Color? greenTint,
  }) =>
      ZzPalette(
        bg: bg ?? this.bg,
        surface: surface ?? this.surface,
        border: border ?? this.border,
        text: text ?? this.text,
        textMuted: textMuted ?? this.textMuted,
        heading: heading ?? this.heading,
        orangeTint: orangeTint ?? this.orangeTint,
        greenTint: greenTint ?? this.greenTint,
      );

  @override
  ZzPalette lerp(ZzPalette? other, double t) {
    if (other == null) return this;
    return ZzPalette(
      bg: Color.lerp(bg, other.bg, t)!,
      surface: Color.lerp(surface, other.surface, t)!,
      border: Color.lerp(border, other.border, t)!,
      text: Color.lerp(text, other.text, t)!,
      textMuted: Color.lerp(textMuted, other.textMuted, t)!,
      heading: Color.lerp(heading, other.heading, t)!,
      orangeTint: Color.lerp(orangeTint, other.orangeTint, t)!,
      greenTint: Color.lerp(greenTint, other.greenTint, t)!,
    );
  }
}

/// Skrót: `context.zz.textMuted` itd.
extension ZzPaletteX on BuildContext {
  ZzPalette get zz => Theme.of(this).extension<ZzPalette>() ?? ZzPalette.light;
}

class ZzTheme {
  static ThemeData light() => _build(Brightness.light, ZzPalette.light);
  static ThemeData dark() => _build(Brightness.dark, ZzPalette.dark);

  static ThemeData _build(Brightness brightness, ZzPalette p) {
    final base = ThemeData(useMaterial3: true, brightness: brightness);
    final textTheme = GoogleFonts.interTextTheme(base.textTheme).copyWith(
      displaySmall: GoogleFonts.poppins(fontWeight: FontWeight.w700, color: p.heading),
      headlineMedium: GoogleFonts.poppins(fontWeight: FontWeight.w700, color: p.heading),
      headlineSmall: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: p.heading),
      titleLarge: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: p.heading),
      titleMedium: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: p.heading),
    ).apply(bodyColor: p.text, displayColor: p.heading);

    final scheme = ColorScheme(
      brightness: brightness,
      primary: ZzColors.orange,
      onPrimary: Colors.white,
      secondary: ZzColors.green,
      onSecondary: Colors.white,
      surface: p.bg,
      onSurface: p.text,
      error: ZzColors.danger,
      onError: Colors.white,
    );

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: p.surface,
      extensions: [p],
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: p.bg,
        foregroundColor: p.heading,
        elevation: 0,
        centerTitle: false,
        titleTextStyle: GoogleFonts.poppins(
            fontWeight: FontWeight.w700, fontSize: 20, color: p.heading),
        surfaceTintColor: Colors.transparent,
      ),
      cardTheme: CardThemeData(
        color: p.bg,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ZzRadius.lg),
          side: BorderSide(color: p.border),
        ),
        margin: EdgeInsets.zero,
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: ZzColors.orange,
          foregroundColor: Colors.white,
          minimumSize: const Size.fromHeight(50),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.md)),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 16),
          elevation: 0,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: p.heading,
          minimumSize: const Size.fromHeight(50),
          side: BorderSide(color: p.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.md)),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 16),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: p.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: BorderSide(color: p.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: BorderSide(color: p.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: const BorderSide(color: ZzColors.orange, width: 1.5),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: p.surface,
        side: BorderSide(color: p.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.sm)),
      ),
      dividerTheme: DividerThemeData(color: p.border, thickness: 1),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.md)),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: p.bg,
        surfaceTintColor: Colors.transparent,
        indicatorColor: p.orangeTint,
        height: 64,
        labelTextStyle: WidgetStateProperty.resolveWith((states) => GoogleFonts.inter(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: states.contains(WidgetState.selected) ? ZzColors.orange : p.textMuted,
            )),
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
              color: states.contains(WidgetState.selected) ? ZzColors.orange : p.textMuted,
            )),
      ),
    );
  }
}
