import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';

/// A circular initials badge — used wherever we'd otherwise need a profile
/// photo we don't have. Never a generic person icon; the initials are
/// always real, derived from the signed-in donor's own name/email. A flat
/// fill, not a gradient — the same restraint contact-book avatars use.
class InitialsAvatar extends StatelessWidget {
  const InitialsAvatar({super.key, required this.initials, this.size = 40});

  final String initials;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
      child: Text(
        initials,
        style: AppText.bodyStrong.copyWith(
          color: Colors.white,
          fontSize: size * 0.38,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
