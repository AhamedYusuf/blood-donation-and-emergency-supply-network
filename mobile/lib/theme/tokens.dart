import 'package:flutter/material.dart';

/// Design tokens for the donor mobile app.
///
/// Shares the palette and restraint of the web staff console (see the
/// repo-root DESIGN.md): one interactive accent (blue), one agent accent
/// (violet), a fixed semantic status set, Inter, a 4px spacing base, and
/// hierarchy from a surface ladder rather than shadows. The mobile layer
/// adds a single elevated surface — the "next donation" hero card — and
/// bottom sheets.
abstract final class AppColors {
  // Ink
  static const ink = Color(0xFF141B2C);
  static const inkSecondary = Color(0xFF3D4A5C);
  static const inkMuted = Color(0xFF6B7684);
  static const inkFaint = Color(0xFF98A1AC);

  // Interactive accent — the only colour a person taps.
  static const primary = Color(0xFF2F5EDB);
  static const primaryPressed = Color(0xFF1F42A8);
  static const primarySubtle = Color(0xFFE8EDFC);
  static const onPrimary = Color(0xFFFFFFFF);

  // Agent accent — marks data produced by the Matching & Dispatch agent.
  static const agent = Color(0xFF7C5CFC);
  static const agentSubtle = Color(0xFFF1EDFF);

  // Surfaces
  static const canvas = Color(0xFFF7F8FA);
  static const surface = Color(0xFFFFFFFF);
  static const surfaceSunken = Color(0xFFEEF1F4);

  // Hairlines
  static const hairline = Color(0xFFDFE3E8);
  static const hairlineStrong = Color(0xFFC6CCD4);

  // Semantic status — colour + word, always. Also the row accent-bar colour.
  static const critical = Color(0xFFD64545);
  static const criticalSubtle = Color(0xFFFBEAEA);
  static const scheduled = Color(0xFFD97F1D); // "urgent" amber on the web
  static const scheduledSubtle = Color(0xFFFCF1E3);
  static const success = Color(0xFF1F8A54);
  static const successSubtle = Color(0xFFE8F5EE);
  static const neutral = Color(0xFF6B7684);
  static const neutralSubtle = Color(0xFFEEF1F4);
}

abstract final class AppSpacing {
  static const xxs = 4.0;
  static const xs = 8.0;
  static const sm = 12.0;
  static const md = 16.0;
  static const lg = 20.0;
  static const xl = 28.0;
  static const xxl = 40.0;

  /// Standard screen side gutter.
  static const gutter = 16.0;
}

abstract final class AppRadii {
  static const sm = 8.0; // buttons, inputs
  static const md = 12.0; // cards, sheets top
  static const lg = 16.0; // hero card
  static const pill = 999.0;
}

abstract final class AppMotion {
  static const fast = Duration(milliseconds: 160);
  static const base = Duration(milliseconds: 240);
  static const curve = Curves.easeOutCubic;
  static const shimmer = Duration(milliseconds: 1400);
}

abstract final class AppElevation {
  /// The one elevated surface in the product — the next-donation hero card
  /// and, reused, bottom sheets.
  static const List<BoxShadow> lifted = [
    BoxShadow(
      color: Color(0x14141B2C), // ~8% ink
      blurRadius: 16,
      offset: Offset(0, 4),
    ),
    BoxShadow(
      color: Color(0x0F141B2C), // ~6% ink
      blurRadius: 3,
      offset: Offset(0, 1),
    ),
  ];
}
