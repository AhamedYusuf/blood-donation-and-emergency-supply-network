import 'package:flutter/material.dart';

import 'tokens.dart';

/// Inter, one family, weights 400/500/600 only — nothing bolder appears
/// anywhere (matches the web system). `tnum` variants are used for every
/// time, count and id fragment so columns line up.
abstract final class AppText {
  static const _f = 'Inter';
  static const _tnum = [FontFeature.tabularFigures()];

  static const display = TextStyle(
      fontFamily: _f, fontSize: 27, fontWeight: FontWeight.w600, height: 1.15, letterSpacing: -0.4, color: AppColors.ink);
  static const title = TextStyle(
      fontFamily: _f, fontSize: 20, fontWeight: FontWeight.w600, height: 1.25, letterSpacing: -0.2, color: AppColors.ink);
  static const headline = TextStyle(
      fontFamily: _f, fontSize: 17, fontWeight: FontWeight.w600, height: 1.3, letterSpacing: -0.1, color: AppColors.ink);
  static const body = TextStyle(
      fontFamily: _f, fontSize: 15, fontWeight: FontWeight.w400, height: 1.45, color: AppColors.inkSecondary);
  static const bodyStrong = TextStyle(
      fontFamily: _f, fontSize: 15, fontWeight: FontWeight.w500, height: 1.4, color: AppColors.ink);
  static const bodySmall = TextStyle(
      fontFamily: _f, fontSize: 13, fontWeight: FontWeight.w400, height: 1.4, color: AppColors.inkMuted);
  static const numeric = TextStyle(
      fontFamily: _f, fontSize: 14, fontWeight: FontWeight.w500, height: 1.3, color: AppColors.ink, fontFeatures: _tnum);
  static const label = TextStyle(
      fontFamily: _f, fontSize: 12, fontWeight: FontWeight.w500, height: 1.3, letterSpacing: 0.4, color: AppColors.inkMuted);
  static const caption = TextStyle(
      fontFamily: _f, fontSize: 11, fontWeight: FontWeight.w500, height: 1.3, letterSpacing: 0.3, color: AppColors.inkFaint);
  static const button = TextStyle(
      fontFamily: _f, fontSize: 15, fontWeight: FontWeight.w600, height: 1.2, letterSpacing: 0);

  /// Big tabular number for the hero card's day-of-month.
  static const heroFigure = TextStyle(
      fontFamily: _f, fontSize: 34, fontWeight: FontWeight.w600, height: 1.0, letterSpacing: -1, color: AppColors.ink, fontFeatures: _tnum);
}

ThemeData buildAppTheme() {
  final scheme = const ColorScheme.light(
    primary: AppColors.primary,
    onPrimary: AppColors.onPrimary,
    secondary: AppColors.agent,
    surface: AppColors.surface,
    onSurface: AppColors.ink,
    error: AppColors.critical,
    outline: AppColors.hairlineStrong,
    outlineVariant: AppColors.hairline,
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: AppColors.canvas,
    fontFamily: AppText._f,
    splashFactory: InkSparkle.splashFactory,
    visualDensity: VisualDensity.standard,

    textTheme: const TextTheme(
      displaySmall: AppText.display,
      titleLarge: AppText.title,
      titleMedium: AppText.headline,
      bodyLarge: AppText.body,
      bodyMedium: AppText.bodySmall,
      labelLarge: AppText.button,
      labelMedium: AppText.label,
      labelSmall: AppText.caption,
    ),

    appBarTheme: const AppBarTheme(
      backgroundColor: AppColors.canvas,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: AppText.title,
      iconTheme: IconThemeData(color: AppColors.ink, size: 22),
    ),

    dividerTheme: const DividerThemeData(
      color: AppColors.hairline,
      thickness: 1,
      space: 1,
    ),

    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.primary,
        foregroundColor: AppColors.onPrimary,
        disabledBackgroundColor: AppColors.primary.withValues(alpha: 0.4),
        disabledForegroundColor: AppColors.onPrimary,
        minimumSize: const Size.fromHeight(50),
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadii.sm)),
        textStyle: AppText.button,
        elevation: 0,
      ),
    ),

    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.ink,
        minimumSize: const Size.fromHeight(50),
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        side: const BorderSide(color: AppColors.hairlineStrong),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadii.sm)),
        textStyle: AppText.button.copyWith(fontWeight: FontWeight.w500),
      ),
    ),

    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.primary,
        textStyle: AppText.button.copyWith(fontWeight: FontWeight.w500),
      ),
    ),

    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: AppColors.surface,
      contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.md),
      hintStyle: AppText.body.copyWith(color: AppColors.inkFaint),
      labelStyle: AppText.body.copyWith(color: AppColors.inkMuted),
      floatingLabelStyle: AppText.bodySmall.copyWith(color: AppColors.primary),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadii.sm),
        borderSide: const BorderSide(color: AppColors.hairlineStrong),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadii.sm),
        borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadii.sm),
        borderSide: const BorderSide(color: AppColors.critical),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadii.sm),
        borderSide: const BorderSide(color: AppColors.critical, width: 1.5),
      ),
      errorStyle: AppText.caption.copyWith(color: AppColors.critical, letterSpacing: 0),
    ),

    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: AppColors.ink,
      contentTextStyle: AppText.bodySmall.copyWith(color: AppColors.surface),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadii.sm)),
    ),

    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: AppColors.surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.md)),
      ),
      showDragHandle: true,
      dragHandleColor: AppColors.hairlineStrong,
    ),

    dialogTheme: DialogThemeData(
      backgroundColor: AppColors.surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadii.md)),
      titleTextStyle: AppText.headline,
      contentTextStyle: AppText.body,
    ),

    progressIndicatorTheme: const ProgressIndicatorThemeData(
      color: AppColors.primary,
      linearMinHeight: 2,
    ),

    pageTransitionsTheme: const PageTransitionsTheme(builders: {
      TargetPlatform.android: _QuietPageTransition(),
      TargetPlatform.iOS: _QuietPageTransition(),
    }),
  );
}

/// A restrained fade-through page transition — no slide, no bounce.
class _QuietPageTransition extends PageTransitionsBuilder {
  const _QuietPageTransition();

  @override
  Widget buildTransitions<T>(
    PageRoute<T> route,
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) {
    final curved = CurvedAnimation(parent: animation, curve: AppMotion.curve);
    return FadeTransition(
      opacity: curved,
      child: SlideTransition(
        position: Tween(begin: const Offset(0, 0.012), end: Offset.zero).animate(curved),
        child: child,
      ),
    );
  }
}
