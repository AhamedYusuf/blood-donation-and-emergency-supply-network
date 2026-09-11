import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// A boarding-pass-style card for the one thing in this app that deserves
/// it: the donor's next confirmed donation. A coloured info panel ([top])
/// and a plain white stub ([bottom], for the cancel action) meet at a
/// perforated notch line — the way a real paper ticket or an Apple Wallet
/// pass is printed, deliberately not just another rounded rectangle,
/// because this is the single piece of information the whole home flow
/// exists to show. The two backgrounds are genuinely different surfaces
/// (not one gradient behind everything) so text and buttons in [bottom]
/// are never read against the brand colour.
///
/// The two semicircle "punches" aren't actually cut out of the card (that
/// needs a clip shaped around content whose height isn't known until
/// layout, which is fragile) — instead they're small circles painted in
/// [notchColor], the colour of whatever sits directly behind the card, so
/// they read as punched holes without depending on exact card geometry.
class TicketCard extends StatelessWidget {
  const TicketCard({
    super.key,
    required this.top,
    this.bottom,
    this.gradient,
    this.color,
    this.notchColor = AppColors.canvas,
    this.dashColor = AppColors.hairlineStrong,
    this.boxShadow,
    this.borderColor,
  });

  final Widget top;
  final Widget? bottom;
  final Gradient? gradient;
  final Color? color;
  final Color notchColor;

  /// Colour of the perforation dashes — default suits a white/light card;
  /// pass a translucent white when [gradient] is a dark brand colour.
  final Color dashColor;
  final List<BoxShadow>? boxShadow;
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        border: borderColor != null ? Border.all(color: borderColor!) : null,
        boxShadow: boxShadow,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          DecoratedBox(
            decoration: BoxDecoration(gradient: gradient, color: gradient == null ? (color ?? AppColors.surface) : null),
            child: top,
          ),
          if (bottom != null) ...[
            ColoredBox(
              color: AppColors.surface,
              child: _NotchDivider(notchColor: notchColor, dashColor: dashColor),
            ),
            ColoredBox(color: AppColors.surface, child: bottom!),
          ],
        ],
      ),
    );
  }
}

class _NotchDivider extends StatelessWidget {
  const _NotchDivider({required this.notchColor, required this.dashColor});
  final Color notchColor;
  final Color dashColor;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 22,
      child: Stack(
        alignment: Alignment.center,
        clipBehavior: Clip.none,
        children: [
          Positioned(
            left: 22,
            right: 22,
            child: CustomPaint(size: const Size(double.infinity, 1), painter: _DashPainter(dashColor)),
          ),
          Positioned(left: -10, child: _Notch(color: notchColor)),
          Positioned(right: -10, child: _Notch(color: notchColor)),
        ],
      ),
    );
  }
}

class _Notch extends StatelessWidget {
  const _Notch({required this.color});
  final Color color;
  @override
  Widget build(BuildContext context) => Container(
        width: 20,
        height: 20,
        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
      );
}

class _DashPainter extends CustomPainter {
  _DashPainter(this.color);
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 1;
    const dashWidth = 5.0, gap = 4.0;
    var x = 0.0;
    while (x < size.width) {
      canvas.drawLine(Offset(x, size.height / 2), Offset(x + dashWidth, size.height / 2), paint);
      x += dashWidth + gap;
    }
  }

  @override
  bool shouldRepaint(_DashPainter oldDelegate) => oldDelegate.color != color;
}
