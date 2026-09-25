import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/blood_requests/create_blood_request_screen.dart';
import 'package:blood_donation_network/features/blood_requests/organization.dart';
import 'package:blood_donation_network/features/blood_requests/organizations_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class _FakeAuthController extends AuthController {
  _FakeAuthController(this._state);

  final AuthState _state;

  @override
  AuthState build() => _state;
}

const _organization = Organization(
  id: 'organization-1',
  name: 'Central Hospital',
  latitude: 24.1,
  longitude: 67.5,
);

Widget _host({List<Organization> organizations = const [_organization]}) =>
    ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(
          () => _FakeAuthController(
            const AuthState(status: AuthStatus.authenticated, role: 'admin'),
          ),
        ),
        bloodRequestOrganizationsProvider.overrideWith(
          (ref) async => organizations,
        ),
      ],
      child: const MaterialApp(home: CreateBloodRequestScreen()),
    );

Future<void> _selectDropdowns(WidgetTester tester) async {
  await tester.pumpAndSettle();
  final dropdowns = find.byType(DropdownButtonFormField<String>);
  await tester.tap(dropdowns.at(0));
  await tester.pumpAndSettle();
  await tester.tap(find.text(_organization.name).last);
  await tester.pumpAndSettle();

  await tester.tap(dropdowns.at(1));
  await tester.pumpAndSettle();
  await tester.tap(find.text('O+').last);
  await tester.pumpAndSettle();

  await tester.tap(dropdowns.at(2));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Urgent').last);
  await tester.pumpAndSettle();
}

Future<void> _fillRequiredFields(WidgetTester tester) async {
  final fields = find.byType(TextFormField);
  await tester.enterText(fields.at(0), '2');
  await tester.enterText(fields.at(1), 'Central Hospital');
  await tester.enterText(fields.at(2), '24.1');
  await tester.enterText(fields.at(3), '67.5');
  await _selectDropdowns(tester);
}

Future<void> _tapCreateRequest(WidgetTester tester) async {
  tester.testTextInput.hide();
  await tester.pump();
  final button = find.widgetWithText(FilledButton, 'Create request');
  await tester.scrollUntilVisible(
    button,
    500,
    scrollable: find.byType(Scrollable).first,
  );
  await tester.tap(button);
  await tester.pump();
}

void main() {
  testWidgets('required fields show validation messages', (tester) async {
    await tester.pumpWidget(_host());
    await _tapCreateRequest(tester);

    expect(find.text('Choose a blood type'), findsOneWidget);
    expect(find.text('Choose an urgency'), findsOneWidget);
    expect(find.text('Enter units requested'), findsOneWidget);
    expect(find.text('Enter a hospital name'), findsOneWidget);
  });

  testWidgets('selected organization populates read-only coordinates', (
    tester,
  ) async {
    await tester.pumpWidget(_host());
    await _selectDropdowns(tester);

    final fields = find.byType(TextFormField);
    expect(tester.widget<TextFormField>(fields.at(2)).controller!.text, '24.1');
    expect(tester.widget<TextFormField>(fields.at(3)).controller!.text, '67.5');
  });

  testWidgets('units outside 1 to 100 are rejected', (tester) async {
    await tester.pumpWidget(_host());
    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), '101');
    await _tapCreateRequest(tester);

    expect(find.text('Enter 1 to 100 units'), findsOneWidget);
  });

  testWidgets('organization without coordinates is rejected', (tester) async {
    await tester.pumpWidget(
      _host(
        organizations: const [
          Organization(id: 'organization-1', name: 'Central Hospital'),
        ],
      ),
    );
    await _fillRequiredFields(tester);
    await _tapCreateRequest(tester);

    expect(
      find.text('The selected organization does not have valid coordinates.'),
      findsOneWidget,
    );
  });
}
