import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The product mark: a blood drop with a plus cut from it (negative space).
/// Geometric, single-colour, no gradient. Used at the top of the login
/// screen and, small, in the app bar.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 44, this.color = AppColors.primary});
  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return SizedBox.square(
      dimension: size,
      child: CustomPaint(painter: _DropPainter(color)),
    );
  }
}

class _DropPainter extends CustomPainter {
  _DropPainter(this.color);
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final drop = Path()
      ..moveTo(w * 0.5, h * 0.04)
      ..cubicTo(w * 0.5, h * 0.04, w * 0.94, h * 0.52, w * 0.94, h * 0.66)
      ..arcToPoint(Offset(w * 0.06, h * 0.66),
          radius: Radius.circular(w * 0.44), clockwise: true)
      ..cubicTo(w * 0.06, h * 0.52, w * 0.5, h * 0.04, w * 0.5, h * 0.04)
      ..close();

    // Plus, carved out.
    final t = w * 0.11; // arm thickness
    final cx = w * 0.5, cy = h * 0.6, arm = w * 0.16;
    final plus = Path()
      ..addRect(Rect.fromLTWH(cx - t / 2, cy - arm, t, arm * 2))
      ..addRect(Rect.fromLTWH(cx - arm, cy - t / 2, arm * 2, t));

    final mark = Path.combine(PathOperation.difference, drop, plus);
    canvas.drawPath(mark, Paint()..color = color..isAntiAlias = true);
  }

  @override
  bool shouldRepaint(_DropPainter old) => old.color != color;
}
