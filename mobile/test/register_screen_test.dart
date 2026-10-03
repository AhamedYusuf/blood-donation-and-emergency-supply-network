import 'package:blood_donation_network/features/auth/register_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

// Covers the validation rules this screen used to get wrong relative to
// the web app's RegisterPage.tsx: a 6-character password with no
// complexity rule and no confirm-password field at all, plus looser
// email/phone/name checks.
Widget _host() => const ProviderScope(
      child: MaterialApp(home: RegisterScreen()),
    );

/// The form is taller than the default 800x600 test surface, pushing the
/// submit button below the fold — tall enough here that every field plus
/// the button are on-screen without needing to scroll mid-test.
void _useTallSurface(WidgetTester tester) {
  tester.view.physicalSize = const Size(800, 2000);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(() {
    tester.view.resetPhysicalSize();
    tester.view.resetDevicePixelRatio();
  });
}

Future<void> _fill(
  WidgetTester tester, {
  String name = 'Jane Smith',
  String email = 'jane@example.com',
  String phone = '+94771234567',
  String password = 'Passw0rd',
  String confirm = 'Passw0rd',
}) async {
  final fields = find.byType(TextFormField);
  await tester.enterText(fields.at(0), name);
  await tester.enterText(fields.at(1), email);
  await tester.enterText(fields.at(2), phone);
  await tester.enterText(fields.at(3), password);
  await tester.enterText(fields.at(4), confirm);
}

void main() {
  testWidgets('has a confirm-password field', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());

    expect(find.text('Confirm password'), findsOneWidget);
    expect(find.byType(TextFormField), findsNWidgets(5));
  });

  testWidgets('rejects a short password even with letters and numbers', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, password: 'abc123', confirm: 'abc123');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Password must be at least 8 characters'), findsOneWidget);
  });

  testWidgets('rejects an 8+ char password with no digit', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, password: 'abcdefgh', confirm: 'abcdefgh');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Must contain at least one letter and one number'), findsOneWidget);
  });

  testWidgets('rejects mismatched confirm password', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, password: 'Passw0rd', confirm: 'Different1');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Passwords do not match'), findsOneWidget);
  });

  testWidgets('rejects a name containing digits', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, name: 'Jane2 Smith');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Name must not contain numbers'), findsOneWidget);
  });

  testWidgets('rejects an email missing a domain dot', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, email: 'jane@example');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a valid email address'), findsOneWidget);
  });

  testWidgets('rejects a phone number that is too short', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester, phone: '123');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a valid phone number (7-15 digits)'), findsOneWidget);
  });

  testWidgets('a fully valid form passes every validator with no errors shown', (tester) async {
    _useTallSurface(tester);
    await tester.pumpWidget(_host());
    await _fill(tester);

    final formState = tester.state<FormState>(find.byType(Form));
    final isValid = formState.validate();
    await tester.pump();

    expect(isValid, isTrue);
    expect(find.text('Enter your full name'), findsNothing);
    expect(find.text('Enter a valid email address'), findsNothing);
    expect(find.text('Enter a valid phone number (7-15 digits)'), findsNothing);
    expect(find.text('Password must be at least 8 characters'), findsNothing);
    expect(find.text('Passwords do not match'), findsNothing);
  });
}
