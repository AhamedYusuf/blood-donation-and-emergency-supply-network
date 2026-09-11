import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// A shimmering placeholder block. Wrap a group of these in [Skeleton] so
/// they share one animation controller.
class SkeletonBox extends StatelessWidget {
  const SkeletonBox({super.key, this.width, this.height = 12, this.radius = 6});
  final double? width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final t = _SkeletonScope.of(context);
    return AnimatedBuilder(
      animation: t,
      builder: (_, _) {
        return Container(
          width: width,
          height: height,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(radius),
            gradient: LinearGradient(
              begin: Alignment(-1 - 2 * (1 - t.value), 0),
              end: Alignment(1 + 2 * t.value, 0),
              colors: const [
                AppColors.surfaceSunken,
                Color(0xFFF4F6F8),
                AppColors.surfaceSunken,
              ],
              stops: const [0.35, 0.5, 0.65],
            ),
          ),
        );
      },
    );
  }
}

class Skeleton extends StatefulWidget {
  const Skeleton({super.key, required this.child});
  final Widget child;

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton> with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: AppMotion.shimmer)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) =>
      _SkeletonScope(controller: _c, child: widget.child);
}

class _SkeletonScope extends InheritedWidget {
  const _SkeletonScope({required this.controller, required super.child});
  final AnimationController controller;

  static AnimationController of(BuildContext context) {
    final scope = context.dependOnInheritedWidgetOfExactType<_SkeletonScope>();
    assert(scope != null, 'SkeletonBox must be inside a Skeleton');
    return scope!.controller;
  }

  @override
  bool updateShouldNotify(_SkeletonScope oldWidget) => false;
}
