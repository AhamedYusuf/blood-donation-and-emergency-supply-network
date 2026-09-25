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

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullName = TextEditingController();
  final _email = TextEditingController();
  final _phoneNumber = TextEditingController();
  final _password = TextEditingController();
  bool _obscure = true;
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _fullName.dispose();
    _email.dispose();
    _phoneNumber.dispose();
    _password.dispose();
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
                    _field('Full name', _fullName, validator: (value) => _required(value, 'Enter your full name')),
                    const SizedBox(height: AppSpacing.md),
                    _field(
                      'Email',
                      _email,
                      keyboardType: TextInputType.emailAddress,
                      validator: (value) {
                        final error = _required(value, 'Enter your email');
                        if (error != null) return error;
                        return value!.contains('@') && value.contains('.') ? null : 'Enter a valid email';
                      },
                    ),
                    const SizedBox(height: AppSpacing.md),
                    _field('Phone number', _phoneNumber, keyboardType: TextInputType.phone,
                        validator: (value) => _required(value, 'Enter your phone number')),
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
                        final error = _required(value, 'Enter a password');
                        if (error != null) return error;
                        return value!.length >= 6 ? null : 'Use at least 6 characters';
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

  String? _required(String? value, String message) =>
      value == null || value.trim().isEmpty ? message : null;
}