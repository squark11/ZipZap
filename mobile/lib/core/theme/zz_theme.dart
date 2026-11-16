import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Tokeny Design Systemu Dowózka.pl — źródło prawdy: `docs/redesign/design-system.md`.
/// Kolory zależne od trybu (jasny/ciemny) pobieraj przez `context.zz`; tu są tylko stałe niezależne od trybu.
class ZzColors {
  /// Turkus marki — ilustracje, tła dekoracyjne, aktywne kropki. NIGDY pod biały tekst (kontrast 2,42:1).
  static const brand = Color(0xFF14B9BA);

  /// Słoneczny akcent — promocje, baner rundy, plakietka koszyka; zawsze z tekstem [onAccent].
  static const accent = Color(0xFFFFB703);
  static const onAccent = Color(0xFF1F2A37);

  /// Grafit — tekst na akcencie, pasek koszyka.
  static const graphite = Color(0xFF1F2A37);

  /// Tekst pomocniczy na grafitowym pasku koszyka (kontrast 10,2:1).
  static const onGraphiteMuted = Color(0xFFC9D6D6);
}

/// Odstępy (skala 4 pt).
class ZzSpace {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 20.0;
  static const xxl = 24.0;
  static const xxxl = 32.0;

  /// Margines ekranu.
  static const screen = 16.0;
}

/// Promienie zaokrągleń.
class ZzRadius {
  static const field = 12.0; // pola formularzy, płytki
  static const card = 20.0; // karty
  static const panel = 28.0; // nagłówek, arkusz dolny
  static const pill = 999.0; // przyciski, chipy, pigułki statusu
}

/// Minimalny rozmiar pola dotyku (WCAG, Material).
const zzMinTouch = 48.0;

/// Punkty łamania układu (szerokość okna).
class ZzBreakpoints {
  static const medium = 600.0; // od tabletu w pionie
  static const expanded = 900.0; // nawigacja boczna
  static const large = 1200.0; // koszyk jako panel boczny

  /// Maks. szerokość treści dla ekranów jednokolumnowych (konto, logowanie, śledzenie).
  static const singleColumn = 640.0;

  /// Maks. szerokość treści stron siatkowych (start, sklep).
  static const content = 1100.0;
}

/// Czasy i krzywe animacji. Przy systemowym „ogranicz ruch" wszystkie czasy = 0 (patrz [ZzMotion.of]).
class ZzMotion {
  static const fast = Duration(milliseconds: 120);
  static const base = Duration(milliseconds: 200);
  static const slow = Duration(milliseconds: 250);
  static const enter = Curves.easeOutCubic;
  static const exit = Curves.easeInCubic;

  /// Czas animacji z uwzględnieniem ustawienia „ogranicz ruch".
  static Duration of(BuildContext context, Duration d) =>
      MediaQuery.maybeDisableAnimationsOf(context) == true ? Duration.zero : d;

  static bool reduced(BuildContext context) => MediaQuery.maybeDisableAnimationsOf(context) == true;
}

/// Kolory zależne od trybu. Pobieraj przez `context.zz`.
@immutable
class ZzPalette extends ThemeExtension<ZzPalette> {
  final Color surface; // tło ekranu
  final Color card; // karty, arkusze, nawigacja, pasek aplikacji
  final Color border; // obrys pól, separatory
  final Color text; // treść
  final Color textMuted; // podpisy, jednostki
  final Color heading; // nagłówki
  final Color brandStrong; // przyciski, linki, aktywna zakładka
  final Color onBrand; // tekst na [brandStrong]
  final Color brandTint; // chipy, licznik, podświetlenie wyboru
  final Color success;
  final Color successTint;
  final Color danger;
  final Color dangerTint;
  final Color warning;
  final Color warningTint;
  final Color info;
  final Color infoTint;
  final Color skeleton; // szkielet ładowania
  final List<BoxShadow> shadowSm; // karty produktów
  final List<BoxShadow> shadowMd; // karty sklepów, panele
  final List<BoxShadow> shadowFloat; // pasek koszyka, pływająca nawigacja

  const ZzPalette({
    required this.surface,
    required this.card,
    required this.border,
    required this.text,
    required this.textMuted,
    required this.heading,
    required this.brandStrong,
    required this.onBrand,
    required this.brandTint,
    required this.success,
    required this.successTint,
    required this.danger,
    required this.dangerTint,
    required this.warning,
    required this.warningTint,
    required this.info,
    required this.infoTint,
    required this.skeleton,
    required this.shadowSm,
    required this.shadowMd,
    required this.shadowFloat,
  });

  static const _ink = Color(0x0F0F2828);

  static const light = ZzPalette(
    surface: Color(0xFFF3F8F8),
    card: Color(0xFFFFFFFF),
    border: Color(0xFFE3EBEB),
    text: Color(0xFF1F2A37),
    textMuted: Color(0xFF5B6675),
    heading: Color(0xFF1F2A37),
    brandStrong: Color(0xFF097778),
    onBrand: Color(0xFFFFFFFF),
    brandTint: Color(0xFFE6F7F7),
    success: Color(0xFF15803D),
    successTint: Color(0xFFE9F7EE),
    danger: Color(0xFFB91C1C),
    dangerTint: Color(0xFFFDECEC),
    warning: Color(0xFFB45309),
    warningTint: Color(0xFFFEF3E2),
    info: Color(0xFF1D4ED8),
    infoTint: Color(0xFFEFF6FF),
    skeleton: Color(0xFFDCE7E6),
    shadowSm: [
      BoxShadow(color: _ink, blurRadius: 2, offset: Offset(0, 1)),
      BoxShadow(color: Color(0x0D0F2828), blurRadius: 18, offset: Offset(0, 6)),
    ],
    shadowMd: [
      BoxShadow(color: _ink, blurRadius: 2, offset: Offset(0, 1)),
      BoxShadow(color: Color(0x140F2828), blurRadius: 24, offset: Offset(0, 8)),
    ],
    shadowFloat: [BoxShadow(color: Color(0x380F2828), blurRadius: 28, offset: Offset(0, 10))],
  );

  static const dark = ZzPalette(
    surface: Color(0xFF0F1717),
    card: Color(0xFF182323),
    border: Color(0xFF263636),
    text: Color(0xFFE8F0F0),
    textMuted: Color(0xFF9FB0B0),
    heading: Color(0xFFF2F6F6),
    brandStrong: Color(0xFF3FD3D4),
    onBrand: Color(0xFF062A2A),
    brandTint: Color(0xFF0F2B2C),
    success: Color(0xFF4ADE80),
    successTint: Color(0xFF12301E),
    danger: Color(0xFFF87171),
    dangerTint: Color(0xFF3A1515),
    warning: Color(0xFFFBBF24),
    warningTint: Color(0xFF3A2A0A),
    info: Color(0xFF93C5FD),
    infoTint: Color(0xFF172554),
    skeleton: Color(0xFF2A3536),
    // W trybie ciemnym karty odróżnia jaśniejsze tło, nie cień.
    shadowSm: [],
    shadowMd: [],
    shadowFloat: [BoxShadow(color: Color(0x66000000), blurRadius: 24, offset: Offset(0, 8))],
  );

  @override
  ZzPalette copyWith({
    Color? surface,
    Color? card,
    Color? border,
    Color? text,
    Color? textMuted,
    Color? heading,
    Color? brandStrong,
    Color? onBrand,
    Color? brandTint,
    Color? success,
    Color? successTint,
    Color? danger,
    Color? dangerTint,
    Color? warning,
    Color? warningTint,
    Color? info,
    Color? infoTint,
    Color? skeleton,
    List<BoxShadow>? shadowSm,
    List<BoxShadow>? shadowMd,
    List<BoxShadow>? shadowFloat,
  }) =>
      ZzPalette(
        surface: surface ?? this.surface,
        card: card ?? this.card,
        border: border ?? this.border,
        text: text ?? this.text,
        textMuted: textMuted ?? this.textMuted,
        heading: heading ?? this.heading,
        brandStrong: brandStrong ?? this.brandStrong,
        onBrand: onBrand ?? this.onBrand,
        brandTint: brandTint ?? this.brandTint,
        success: success ?? this.success,
        successTint: successTint ?? this.successTint,
        danger: danger ?? this.danger,
        dangerTint: dangerTint ?? this.dangerTint,
        warning: warning ?? this.warning,
        warningTint: warningTint ?? this.warningTint,
        info: info ?? this.info,
        infoTint: infoTint ?? this.infoTint,
        skeleton: skeleton ?? this.skeleton,
        shadowSm: shadowSm ?? this.shadowSm,
        shadowMd: shadowMd ?? this.shadowMd,
        shadowFloat: shadowFloat ?? this.shadowFloat,
      );

  @override
  ZzPalette lerp(ZzPalette? other, double t) {
    if (other == null) return this;
    Color c(Color a, Color b) => Color.lerp(a, b, t)!;
    return ZzPalette(
      surface: c(surface, other.surface),
      card: c(card, other.card),
      border: c(border, other.border),
      text: c(text, other.text),
      textMuted: c(textMuted, other.textMuted),
      heading: c(heading, other.heading),
      brandStrong: c(brandStrong, other.brandStrong),
      onBrand: c(onBrand, other.onBrand),
      brandTint: c(brandTint, other.brandTint),
      success: c(success, other.success),
      successTint: c(successTint, other.successTint),
      danger: c(danger, other.danger),
      dangerTint: c(dangerTint, other.dangerTint),
      warning: c(warning, other.warning),
      warningTint: c(warningTint, other.warningTint),
      info: c(info, other.info),
      infoTint: c(infoTint, other.infoTint),
      skeleton: c(skeleton, other.skeleton),
      shadowSm: t < 0.5 ? shadowSm : other.shadowSm,
      shadowMd: t < 0.5 ? shadowMd : other.shadowMd,
      shadowFloat: t < 0.5 ? shadowFloat : other.shadowFloat,
    );
  }
}

/// Skrót: `context.zz.textMuted` itd.
extension ZzPaletteX on BuildContext {
  ZzPalette get zz => Theme.of(this).extension<ZzPalette>() ?? ZzPalette.light;
}

/// Kroje pisma — pliki w `assets/fonts` (bez pobierania z sieci przy starcie).
class ZzFonts {
  static const heading = 'Poppins';
  static const body = 'Inter';
}

class ZzTheme {
  static ThemeData light() => _build(Brightness.light, ZzPalette.light);
  static ThemeData dark() => _build(Brightness.dark, ZzPalette.dark);

  static ThemeData _build(Brightness brightness, ZzPalette p) {
    final base = ThemeData(useMaterial3: true, brightness: brightness, fontFamily: ZzFonts.body);
    TextStyle h(FontWeight w, double size, [double height = 1.25]) =>
        TextStyle(fontFamily: ZzFonts.heading, fontWeight: w, fontSize: size, height: height, color: p.heading);
    final textTheme = base.textTheme
        .copyWith(
          displaySmall: h(FontWeight.w700, 32, 1.2),
          headlineMedium: h(FontWeight.w700, 28, 1.2),
          headlineSmall: h(FontWeight.w700, 24, 1.2),
          titleLarge: h(FontWeight.w600, 20),
          titleMedium: h(FontWeight.w600, 17),
          bodyLarge: const TextStyle(fontFamily: ZzFonts.body, fontSize: 16, height: 1.5),
          bodyMedium: const TextStyle(fontFamily: ZzFonts.body, fontSize: 14, height: 1.45),
          bodySmall: TextStyle(fontFamily: ZzFonts.body, fontSize: 13, height: 1.4, fontWeight: FontWeight.w500, color: p.textMuted),
          labelLarge: const TextStyle(fontFamily: ZzFonts.body, fontSize: 15, fontWeight: FontWeight.w600),
          labelSmall: const TextStyle(fontFamily: ZzFonts.body, fontSize: 12, fontWeight: FontWeight.w600, letterSpacing: 0.3),
        )
        .apply(bodyColor: p.text, displayColor: p.heading);

    final scheme = ColorScheme(
      brightness: brightness,
      primary: p.brandStrong,
      onPrimary: p.onBrand,
      primaryContainer: p.brandTint,
      onPrimaryContainer: p.brandStrong,
      secondary: ZzColors.accent,
      onSecondary: ZzColors.onAccent,
      surface: p.card,
      onSurface: p.text,
      onSurfaceVariant: p.textMuted,
      outline: p.border,
      error: p.danger,
      onError: brightness == Brightness.light ? Colors.white : const Color(0xFF2A0A0A),
    );

    final pill = RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.pill));
    const buttonText = TextStyle(fontFamily: ZzFonts.body, fontWeight: FontWeight.w700, fontSize: 16);
    OutlineInputBorder field(Color c, [double w = 1.5]) => OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.field),
          borderSide: BorderSide(color: c, width: w),
        );

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: p.surface,
      extensions: [p],
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: p.surface,
        foregroundColor: p.heading,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: h(FontWeight.w700, 22),
        surfaceTintColor: Colors.transparent,
        // Pasek stanu w kolorze tła ekranu (w trybie ciemnym nie turkus — test na telefonie 2026-11-14).
        systemOverlayStyle: brightness == Brightness.light
            ? SystemUiOverlayStyle.dark.copyWith(statusBarColor: p.surface)
            : SystemUiOverlayStyle.light.copyWith(statusBarColor: p.surface),
      ),
      cardTheme: CardThemeData(
        color: p.card,
        elevation: brightness == Brightness.light ? 1 : 0,
        shadowColor: const Color(0x330F2828),
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.card)),
        margin: EdgeInsets.zero,
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: p.brandStrong,
          foregroundColor: p.onBrand,
          disabledBackgroundColor: p.border,
          disabledForegroundColor: p.textMuted,
          minimumSize: const Size.fromHeight(52),
          shape: pill,
          textStyle: buttonText,
          elevation: 0,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: p.brandStrong,
          foregroundColor: p.onBrand,
          disabledBackgroundColor: p.border,
          disabledForegroundColor: p.textMuted,
          minimumSize: const Size(zzMinTouch, zzMinTouch),
          shape: pill,
          textStyle: buttonText,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: p.heading,
          minimumSize: const Size.fromHeight(52),
          side: BorderSide(color: p.border, width: 1.5),
          shape: pill,
          textStyle: buttonText.copyWith(fontWeight: FontWeight.w600),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: p.brandStrong,
          minimumSize: const Size(zzMinTouch, zzMinTouch),
          textStyle: const TextStyle(fontFamily: ZzFonts.body, fontWeight: FontWeight.w600, fontSize: 15),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(minimumSize: const Size(zzMinTouch, zzMinTouch)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: p.card,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        border: field(p.border),
        enabledBorder: field(p.border),
        focusedBorder: field(p.brandStrong),
        errorBorder: field(p.danger),
        focusedErrorBorder: field(p.danger, 2),
        hintStyle: TextStyle(color: p.textMuted),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: p.card,
        selectedColor: p.brandTint,
        side: BorderSide(color: p.border),
        shape: pill,
        labelStyle: TextStyle(color: p.text, fontWeight: FontWeight.w500),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: p.brandStrong),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? p.brandStrong : null),
        checkColor: WidgetStatePropertyAll(p.onBrand),
      ),
      radioTheme: RadioThemeData(
        fillColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? p.brandStrong : p.textMuted),
      ),
      dividerTheme: DividerThemeData(color: p.border, thickness: 1),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: ZzColors.graphite,
        contentTextStyle: const TextStyle(fontFamily: ZzFonts.body, color: Colors.white, fontSize: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: p.card,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(ZzRadius.panel))),
        showDragHandle: true,
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: p.card,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.panel)),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: p.card,
        surfaceTintColor: Colors.transparent,
        indicatorColor: p.brandTint,
        height: 68,
        labelTextStyle: WidgetStateProperty.resolveWith((states) => TextStyle(
              fontFamily: ZzFonts.body,
              fontSize: 11, // 12 px łamało „Zamówienia" na dwa wiersze przy 360 px
              letterSpacing: 0,
              fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w600,
              color: states.contains(WidgetState.selected) ? p.brandStrong : p.textMuted,
            )),
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
              color: states.contains(WidgetState.selected) ? p.brandStrong : p.textMuted,
            )),
      ),
      navigationRailTheme: NavigationRailThemeData(
        backgroundColor: p.card,
        indicatorColor: p.brandTint,
        selectedIconTheme: IconThemeData(color: p.brandStrong),
        unselectedIconTheme: IconThemeData(color: p.textMuted),
        selectedLabelTextStyle:
            TextStyle(fontFamily: ZzFonts.body, fontWeight: FontWeight.w700, fontSize: 13, color: p.brandStrong),
        unselectedLabelTextStyle:
            TextStyle(fontFamily: ZzFonts.body, fontWeight: FontWeight.w600, fontSize: 13, color: p.textMuted),
      ),
    );
  }
}
