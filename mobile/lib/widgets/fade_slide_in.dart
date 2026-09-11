import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// Wraps a child in a one-shot fade + rise-in. Pass an increasing [index]
/// across a list to get a staggered cascade (each item starts a little
/// after the last) — the small bit of motion that makes a list feel
/// composed rather than dumped on screen, without a shimmer/parallax
/// gimmick attached to it.
class FadeSlideIn extends StatefulWidget {
  const FadeSlideIn({super.key, required this.child, this.index = 0});

  final Widget child;
  final int index;

  @override
  State<FadeSlideIn> createState() => _FadeSlideInState();
}

class _FadeSlideInState extends State<FadeSlideIn> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: AppMotion.base,
  );
  late final Animation<double> _opacity = CurvedAnimation(parent: _controller, curve: Curves.easeOut);
  late final Animation<Offset> _offset = Tween(
    begin: const Offset(0, 0.06),
    end: Offset.zero,
  ).animate(CurvedAnimation(parent: _controller, curve: AppMotion.curve));

  @override
  void initState() {
    super.initState();
    final delay = Duration(milliseconds: 35 * widget.index.clamp(0, 12));
    Future.delayed(delay, () {
      if (mounted) _controller.forward();
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: _opacity,
      child: SlideTransition(position: _offset, child: widget.child),
    );
  }
}
