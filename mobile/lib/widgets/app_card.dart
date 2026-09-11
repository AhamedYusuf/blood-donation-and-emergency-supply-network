import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// Flat surface card: white on canvas, a uniform hairline border, no
/// shadow. `accent` paints a 3px strip down the leading edge (status
/// colour on list rows). `elevated` opts into the single lifted treatment
/// — reserved for the next-donation hero card.
class AppCard extends StatelessWidget {
  const AppCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(AppSpacing.md),
    this.accent,
    this.elevated = false,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? accent;
  final bool elevated;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(elevated ? AppRadii.lg : AppRadii.md);

    Widget inner = Padding(padding: padding, child: child);
    if (accent != null) {
      inner = Stack(
        children: [
          Positioned(top: 0, bottom: 0, left: 0, width: 3, child: ColoredBox(color: accent!)),
          inner,
        ],
      );
    }
    if (onTap != null) {
      inner = Material(
        color: Colors.transparent,
        child: InkWell(onTap: onTap, child: inner),
      );
    }

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: radius,
        border: elevated ? null : Border.all(color: AppColors.hairline),
        boxShadow: elevated ? AppElevation.lifted : null,
      ),
      child: inner,
    );
  }
}
