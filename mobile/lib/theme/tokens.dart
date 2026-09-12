import 'package:flutter/material.dart';

/// Design tokens for the donor mobile app — the exact palette and scale
/// from the repo-root DESIGN.md (§4, §7.1, §39), shared with the web
/// staff console so both surfaces read as one product: burgundy brand
/// identity, dark navy for text, soft-gray canvas, white cards. Plain
/// grouped-list surfaces (no dark gradients, no blur, no glow) — the one
/// exception is the next-donation ticket card, which uses the brand's own
/// red family the way a physical donor card would.
abstract final class AppColors {
  // Neutrals — DESIGN.md §4.2
  static const ink = Color(0xFF172033); // Dark Navy
  static const inkSecondary = Color(0xFF3D4759);
  static const inkMuted = Color(0xFF5E6675); // Secondary Text
  static const inkFaint = Color(0xFF8A92A1); // Light Text

  // Interactive accent — DESIGN.md §4.1 Primary Burgundy. The only
  // colour a person taps.
  static const primary = Color(0xFF9F1738); // Primary Burgundy
  static const primaryPressed = Color(0xFF7F1230); // Dark Burgundy
  static const primarySubtle = Color(0xFFFFF1F4); // Light Red
  static const onPrimary = Color(0xFFFFFFFF);

  // AI/agent accent — DESIGN.md §30: a subtle blue reserved for marking
  // data the Matching & Dispatch agent produced, distinct from both the
  // brand red and the semantic status colours.
  static const agent = Color(0xFF2563EB); // Informational
  static const agentSubtle = Color(0xFFEFF6FF);

  // Surfaces — DESIGN.md §4.2
  static const canvas = Color(0xFFF6F7F9); // Background
  static const surface = Color(0xFFFFFFFF);
  static const surfaceSunken = Color(0xFFEEF0F3);

  // Hairlines
  static const hairline = Color(0xFFE7E9EE); // Border
  static const hairlineStrong = Color(0xFFD5D9E0);

  // Semantic status — DESIGN.md §5. `critical` (Blood Red) sits in the
  // same family as `primary` (Burgundy) on purpose — this is a blood
  // app, red-for-alert is the honest choice — but is brighter and more
  // saturated so the two are never mistaken for each other side by side.
  static const critical = Color(0xFFC62845); // Blood Red
  static const criticalSubtle = Color(0xFFFFF0F2);
  static const scheduled = Color(0xFFD97706); // Warning
  static const scheduledSubtle = Color(0xFFFFF7E8);
  static const success = Color(0xFF198754);
  static const successSubtle = Color(0xFFEAF7F0);
  static const neutral = Color(0xFF5E6675);
  static const neutralSubtle = Color(0xFFEEF0F3);
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

/// DESIGN.md §39: small 8px, standard 12-14px, cards 14-18px, large hero
/// surfaces 18-24px.
abstract final class AppRadii {
  static const sm = 10.0; // buttons, inputs, list cards
  static const md = 14.0; // grouped cards, sheets top
  static const lg = 18.0; // the one boarding-pass card
  static const pill = 999.0;
}

/// DESIGN.md §36: 150-300ms standard, 300-500ms page transitions.
abstract final class AppMotion {
  static const fast = Duration(milliseconds: 150);
  static const base = Duration(milliseconds: 250);
  static const curve = Curves.easeOutCubic;
  static const shimmer = Duration(milliseconds: 1400);
}

abstract final class AppElevation {
  /// The one lifted surface in the product — the next-donation ticket
  /// card and, reused, bottom sheets. A plain neutral shadow, not a
  /// coloured glow — restraint over spectacle (DESIGN.md §65: "soft
  /// shadows", not excessive decoration).
  static const List<BoxShadow> lifted = [
    BoxShadow(
      color: Color(0x1D172033), // ~11% ink
      blurRadius: 20,
      offset: Offset(0, 6),
    ),
    BoxShadow(
      color: Color(0x0F172033), // ~6% ink
      blurRadius: 3,
      offset: Offset(0, 1),
    ),
  ];
}

/// The single gradient in the product, reserved for the next-donation
/// ticket card — nowhere else. Dark Burgundy → Burgundy → Blood Red,
/// diagonal, no glow or blur behind it: the brand's own three reds
/// (DESIGN.md §4.1), not an invented colour.
abstract final class AppGradients {
  static const ticket = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xFF7F1230), Color(0xFF9F1738), Color(0xFFC62845)],
    stops: [0.0, 0.55, 1.0],
  );
}
