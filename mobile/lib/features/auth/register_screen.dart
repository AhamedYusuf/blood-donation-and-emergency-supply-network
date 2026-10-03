import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/brand_mark.dart';
import 'auth_controller.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

// Mirrors the web app's RegisterPage.tsx validation exactly (EMAIL_RE /
// PHONE_RE / validateFullName / validatePassword) — these used to be
// looser on mobile (6-char password with no complexity rule, no format
// checks on email/phone/name, no confirm-password field at all), so a
// donor could end up with a weaker account via mobile than the same
// sign-up would ever allow on web.
final _emailRe = RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$');
final _phoneRe = RegExp(r'^\+?[\d\s\-]{7,15}$');

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullName = TextEditingController();
  final _email = TextEditingController();
  final _phoneNumber = TextEditingController();
  final _password = TextEditingController();
  final _confirmPassword = TextEditingController();
  bool _obscure = true;
  bool _obscureConfirm = true;
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _fullName.dispose();
    _email.dispose();
    _phoneNumber.dispose();
    _password.dispose();
    _confirmPassword.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).register(
            fullName: _fullName.text.trim(),
            email: _email.text.trim(),
            phoneNumber: _phoneNumber.text.trim(),
            password: _password.text,
          );
    } on ApiException catch (e) {
      setState(() => _error = e.statusCode == 409
          ? 'An account with these details already exists.'
          : e.message);
    } catch (_) {
      setState(() => _error = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.xl, AppSpacing.xxl, AppSpacing.xl, AppSpacing.xl),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(
                      child: Container(
                        width: 64,
                        height: 64,
                        decoration: const BoxDecoration(color: AppColors.primarySubtle, shape: BoxShape.circle),
                        child: const Center(child: BrandMark(size: 32)),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    Text('Create your donor account', style: AppText.title, textAlign: TextAlign.center),
                    const SizedBox(height: AppSpacing.xxl),
                    _field(
                      'Full name',
                      _fullName,
                      validator: (value) {
                        final trimmed = value?.trim() ?? '';
                        if (trimmed.isEmpty) return 'Enter your full name';
                        if (trimmed.length < 2) return 'Must be at least 2 characters';
                        if (RegExp(r'\d').hasMatch(trimmed)) return 'Name must not contain numbers';
                        return null;
                      },
                    ),
                    const SizedBox(height: AppSpacing.md),
                    _field(
                      'Email',
                      _email,
                      keyboardType: TextInputType.emailAddress,
                      validator: (value) {
                        final trimmed = value?.trim() ?? '';
                        if (trimmed.isEmpty) return 'Enter your email';
                        return _emailRe.hasMatch(trimmed) ? null : 'Enter a valid email address';
                      },
                    ),
                    const SizedBox(height: AppSpacing.md),
                    _field(
                      'Phone number',
                      _phoneNumber,
                      keyboardType: TextInputType.phone,
                      validator: (value) {
                        final trimmed = value?.trim() ?? '';
                        if (trimmed.isEmpty) return 'Enter your phone number';
                        return _phoneRe.hasMatch(trimmed)
                            ? null
                            : 'Enter a valid phone number (7-15 digits)';
                      },
                    ),
                    const SizedBox(height: AppSpacing.md),
                    _field(
                      'Password',
                      _password,
                      obscureText: _obscure,
                      suffixIcon: IconButton(
                        icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                        onPressed: () => setState(() => _obscure = !_obscure),
                      ),
                      validator: (value) {
                        if (value == null || value.isEmpty) return 'Enter a password';
                        if (value.length < 8) return 'Password must be at least 8 characters';
                        if (!RegExp(r'(?=.*[a-zA-Z])(?=.*\d)').hasMatch(value)) {
                          return 'Must contain at least one letter and one number';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: AppSpacing.xxs),
                    Text(
                      'Min. 8 characters, including a letter and a number.',
                      style: AppText.caption.copyWith(color: AppColors.inkFaint),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    _field(
                      'Confirm password',
                      _confirmPassword,
                      obscureText: _obscureConfirm,
                      suffixIcon: IconButton(
                        icon: Icon(
                            _obscureConfirm ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                        onPressed: () => setState(() => _obscureConfirm = !_obscureConfirm),
                      ),
                      validator: (value) {
                        if (value == null || value.isEmpty) return 'Please confirm your password';
                        return value == _password.text ? null : 'Passwords do not match';
                      },
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: AppSpacing.md),
                      Text(_error!, style: AppText.bodySmall.copyWith(color: AppColors.critical)),
                    ],
                    const SizedBox(height: AppSpacing.lg),
                    FilledButton(
                      onPressed: _submitting ? null : _submit,
                      child: _submitting
                          ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Create account'),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    TextButton(
                      onPressed: () => context.go('/login'),
                      child: const Text('Already have an account? Sign in'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _field(
    String label,
    TextEditingController controller, {
    TextInputType? keyboardType,
    bool obscureText = false,
    Widget? suffixIcon,
    String? Function(String?)? validator,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.xs, left: 2),
          child: Text(label, style: AppText.label),
        ),
        TextFormField(
          controller: controller,
          keyboardType: keyboardType,
          obscureText: obscureText,
          decoration: InputDecoration(suffixIcon: suffixIcon),
          validator: validator,
        ),
      ],
    );
  }
}