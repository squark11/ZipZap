import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Tokeny marki ZipZap (z /branding).
class ZzColors {
  static const orange = Color(0xFFF97316); // primary
  static const orange600 = Color(0xFFEA6A0C);
  static const orange50 = Color(0xFFFFF3EA);
  static const green = Color(0xFF22C55E); // secondary / sukces
  static const green50 = Color(0xFFE9FBF0);
  static const graphite = Color(0xFF3A3F4B); // tekst / wordmark
  static const bg = Color(0xFFFFFFFF);
  static const surface = Color(0xFFF7F8FA);
  static const border = Color(0xFFE5E7EB);
  static const text = Color(0xFF3A3F4B);
  static const textMuted = Color(0xFF6B7280);
  static const danger = Color(0xFFEF4444);
  static const warning = Color(0xFFF59E0B);
}

class ZzRadius {
  static const sm = 6.0;
  static const md = 10.0;
  static const lg = 16.0;
}

class ZzTheme {
  static ThemeData light() {
    final base = ThemeData(useMaterial3: true, brightness: Brightness.light);
    final textTheme = GoogleFonts.interTextTheme(base.textTheme).copyWith(
      displaySmall: GoogleFonts.poppins(fontWeight: FontWeight.w700, color: ZzColors.graphite),
      headlineMedium: GoogleFonts.poppins(fontWeight: FontWeight.w700, color: ZzColors.graphite),
      headlineSmall: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: ZzColors.graphite),
      titleLarge: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: ZzColors.graphite),
      titleMedium: GoogleFonts.poppins(fontWeight: FontWeight.w600, color: ZzColors.graphite),
    ).apply(bodyColor: ZzColors.text, displayColor: ZzColors.graphite);

    final scheme = const ColorScheme.light(
      primary: ZzColors.orange,
      onPrimary: Colors.white,
      secondary: ZzColors.green,
      onSecondary: Colors.white,
      surface: ZzColors.bg,
      onSurface: ZzColors.text,
      error: ZzColors.danger,
    );

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: ZzColors.surface,
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: ZzColors.bg,
        foregroundColor: ZzColors.graphite,
        elevation: 0,
        centerTitle: false,
        titleTextStyle: GoogleFonts.poppins(
            fontWeight: FontWeight.w700, fontSize: 20, color: ZzColors.graphite),
        surfaceTintColor: Colors.transparent,
      ),
      cardTheme: CardThemeData(
        color: ZzColors.bg,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ZzRadius.lg),
          side: const BorderSide(color: ZzColors.border),
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
          foregroundColor: ZzColors.graphite,
          minimumSize: const Size.fromHeight(50),
          side: const BorderSide(color: ZzColors.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.md)),
          textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600, fontSize: 16),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: ZzColors.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: const BorderSide(color: ZzColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: const BorderSide(color: ZzColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ZzRadius.md),
          borderSide: const BorderSide(color: ZzColors.orange, width: 1.5),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: ZzColors.surface,
        side: const BorderSide(color: ZzColors.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.sm)),
      ),
      dividerTheme: const DividerThemeData(color: ZzColors.border, thickness: 1),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ZzRadius.md)),
      ),
    );
  }
}
