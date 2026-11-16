import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../theme/zz_theme.dart';

/// Rodzaj komunikatu.
enum ZzNoticeKind { info, success, warning, danger }

/// Ramka komunikatu (informacja / sukces / uwaga / błąd) — zamiast ręcznie budowanych kontenerów na ekranach.
class ZzNotice extends StatelessWidget {
  final ZzNoticeKind kind;
  final String? title;
  final String message;
  final Widget? action;
  final EdgeInsetsGeometry margin;

  const ZzNotice({
    super.key,
    required this.kind,
    required this.message,
    this.title,
    this.action,
    this.margin = EdgeInsets.zero,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final (bg, fg) = switch (kind) {
      ZzNoticeKind.info => (p.brandTint, p.brandStrong),
      ZzNoticeKind.success => (p.successTint, p.success),
      ZzNoticeKind.warning => (p.warningTint, p.warning),
      ZzNoticeKind.danger => (p.dangerTint, p.danger),
    };
    return Semantics(
      liveRegion: kind == ZzNoticeKind.danger,
      child: Container(
        width: double.infinity,
        margin: margin,
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(16)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (title != null) ...[
              Text(title!, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: fg)),
              const SizedBox(height: 4),
            ],
            Text(message,
                style: TextStyle(
                  color: title == null ? fg : p.text,
                  fontWeight: title == null ? FontWeight.w600 : FontWeight.w400,
                  height: 1.4,
                )),
            if (action != null) ...[const SizedBox(height: 10), action!],
          ],
        ),
      ),
    );
  }
}

/// Nagłówek sekcji z opcjonalnym linkiem po prawej („Wszystkie").
class ZzSectionHeader extends StatelessWidget {
  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;
  const ZzSectionHeader({super.key, required this.title, this.actionLabel, this.onAction});

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Expanded(child: Semantics(header: true, child: Text(title, style: Theme.of(context).textTheme.titleMedium))),
          if (actionLabel != null) TextButton(onPressed: onAction, child: Text(actionLabel!)),
        ],
      );
}

/// Pole wyszukiwania (pigułka z ikoną lupy).
class ZzSearchField extends StatelessWidget {
  final TextEditingController? controller;
  final String hint;
  final ValueChanged<String>? onChanged;
  final bool autofocus;
  const ZzSearchField({super.key, this.controller, required this.hint, this.onChanged, this.autofocus = false});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    final pill = OutlineInputBorder(
      borderRadius: BorderRadius.circular(ZzRadius.pill),
      borderSide: BorderSide(color: p.border, width: 1.5),
    );
    return TextField(
      controller: controller,
      onChanged: onChanged,
      autofocus: autofocus,
      textInputAction: TextInputAction.search,
      decoration: InputDecoration(
        hintText: hint,
        prefixIcon: Icon(Icons.search, color: p.textMuted),
        filled: true,
        fillColor: p.card,
        border: pill,
        enabledBorder: pill,
        focusedBorder: pill.copyWith(borderSide: BorderSide(color: p.brandStrong, width: 1.5)),
        contentPadding: const EdgeInsets.symmetric(vertical: 14, horizontal: 18),
      ),
    );
  }
}

/// Chip kategorii: kółko z ikoną (assets/svg/categories) i podpis pod spodem.
class ZzCategoryChip extends StatelessWidget {
  final String label;

  /// Nazwa pliku w `assets/svg/categories/` bez rozszerzenia (np. „owoce").
  final String icon;
  final bool selected;
  final VoidCallback? onTap;
  const ZzCategoryChip({super.key, required this.label, required this.icon, this.selected = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = context.zz;
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: SizedBox(
          width: 72,
          child: Column(
            children: [
              AnimatedContainer(
                duration: ZzMotion.of(context, ZzMotion.base),
                curve: ZzMotion.enter,
                width: 58,
                height: 58,
                decoration: BoxDecoration(
                  color: selected ? p.brandStrong : p.brandTint,
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: SvgPicture.asset(
                  'assets/svg/categories/$icon.svg',
                  width: 26,
                  height: 26,
                  colorFilter: ColorFilter.mode(selected ? p.onBrand : p.brandStrong, BlendMode.srcIn),
                ),
              ),
              const SizedBox(height: 6),
              Text(label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, fontWeight: selected ? FontWeight.w700 : FontWeight.w500)),
            ],
          ),
        ),
      ),
    );
  }
}
