// Dev-only preview gallery. Run with:
//   flutter run -t lib/dev_gallery.dart
// Renders every donor screen with mock data — no backend, no login.
// Not wired into the real app (main.dart) and safe to delete.
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'features/appointments/appointment.dart';
import 'features/appointments/appointments_repository.dart';
import 'features/appointments/book_appointment_screen.dart';
import 'features/appointments/my_appointments_screen.dart';
import 'features/auth/auth_controller.dart';
import 'features/auth/login_screen.dart';
import 'features/home/home_screen.dart';
import 'theme/app_theme.dart';
import 'theme/tokens.dart';

void main() {
  final now = DateTime.now();
  final sample = <Appointment>[
    Appointment(
        id: '1', donorId: 'd', organizationId: 'o',
        scheduledTime: now.add(const Duration(days: 2, hours: 4)),
        status: 'scheduled', relatedWorkflowId: 'w1', donorBloodType: 'O+'),
    Appointment(
        id: '2', donorId: 'd', organizationId: 'o',
        scheduledTime: now.add(const Duration(days: 9, hours: 1)),
        status: 'scheduled', donorBloodType: 'O+'),
    Appointment(
        id: '3', donorId: 'd', organizationId: 'o',
        scheduledTime: now.subtract(const Duration(days: 12)),
        status: 'completed', donorBloodType: 'O+', unitsDonated: 1),
    Appointment(
        id: '4', donorId: 'd', organizationId: 'o',
        scheduledTime: now.subtract(const Duration(days: 40)),
        status: 'no_show', donorBloodType: 'O+'),
    Appointment(
        id: '5', donorId: 'd', organizationId: 'o',
        scheduledTime: now.subtract(const Duration(days: 63)),
        status: 'cancelled', donorBloodType: 'O+'),
  ];
  final banks = [
    BloodBank(id: 'o1', name: 'Colombo National Blood Bank', address: 'Narahenpita, Colombo 05'),
    BloodBank(id: 'o2', name: 'Kandy General Blood Bank', address: 'William Gopallawa Mw, Kandy'),
    BloodBank(id: 'o3', name: 'Galle District Blood Bank', address: 'Karapitiya, Galle'),
  ];

  runApp(ProviderScope(
    overrides: [
      currentUserIdProvider.overrideWithValue('donor-1'),
      myAppointmentsProvider.overrideWith((ref) async => sample),
      bloodBanksProvider.overrideWith((ref) async => banks),
    ],
    child: MaterialApp(
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home: const _Gallery(sampleEmpty: false),
    ),
  ));
}

class _Gallery extends ConsumerWidget {
  const _Gallery({required this.sampleEmpty});
  final bool sampleEmpty;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    void open(Widget screen) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));

    Widget tile(String label, VoidCallback onTap) => Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.xs),
          child: FilledButton(
            onPressed: onTap,
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.surface,
              foregroundColor: AppColors.ink,
              alignment: Alignment.centerLeft,
              side: const BorderSide(color: AppColors.hairline),
            ),
            child: Text(label),
          ),
        );

    return Scaffold(
      appBar: AppBar(title: Text('Preview gallery', style: AppText.title)),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.gutter),
        children: [
          Text('Donor screens with mock data', style: AppText.body),
          const SizedBox(height: AppSpacing.md),
          tile('Home', () => open(const HomeScreen())),
          tile('My donations — with history', () => open(const MyAppointmentsScreen())),
          tile('My donations — empty', () => open(
                ProviderScope(
                  overrides: [
                    currentUserIdProvider.overrideWithValue('donor-1'),
                    myAppointmentsProvider.overrideWith((ref) async => <Appointment>[]),
                  ],
                  child: const MyAppointmentsScreen(),
                ),
              )),
          tile('Book a donation', () => open(const BookAppointmentScreen())),
          tile('Login', () => open(const LoginScreen())),
        ],
      ),
    );
  }
}
