import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'appointment.dart';

class AppointmentsRepository {
  AppointmentsRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/appointments/donor/{donorId} — the caller's own history.
  Future<List<Appointment>> myAppointments(String donorId) async {
    final json = await _api.get('/api/appointments/donor/$donorId', token: _token);
    final list = (json as List).cast<Map<String, dynamic>>();
    final appointments = list.map(Appointment.fromJson).toList()
      ..sort((a, b) => b.scheduledTime.compareTo(a.scheduledTime));
    return appointments;
  }

  /// POST /api/appointments — donor books a slot.
  Future<Appointment> book({
    required String organizationId,
    required DateTime scheduledTime,
  }) async {
    final json = await _api.post(
      '/api/appointments',
      token: _token,
      body: {
        'organizationId': organizationId,
        'scheduledTime': scheduledTime.toUtc().toIso8601String(),
        'relatedWorkflowId': null,
      },
    );
    return Appointment.fromJson(json as Map<String, dynamic>);
  }

  /// PUT /api/appointments/{id}/status — a donor may only cancel.
  Future<void> cancel(String appointmentId) async {
    await _api.put(
      '/api/appointments/$appointmentId/status',
      token: _token,
      body: {'newStatus': 'cancelled'},
    );
  }

  /// GET /api/organizations, narrowed to blood banks for the booking picker.
  Future<List<BloodBank>> bloodBanks() async {
    final json = await _api.get('/api/organizations', token: _token);
    final list = (json as List).cast<Map<String, dynamic>>();
    return list
        .where((o) => (o['type'] as String?)?.toLowerCase() == 'blood_bank')
        .map(BloodBank.fromJson)
        .toList();
  }
}

final appointmentsRepositoryProvider =
    Provider<AppointmentsRepository>((ref) => AppointmentsRepository(ref));

/// The signed-in donor's appointments. Auto-disposed so it refetches when
/// the screen is revisited; `ref.invalidate` forces an immediate refresh.
final myAppointmentsProvider =
    FutureProvider.autoDispose<List<Appointment>>((ref) async {
  final donorId = ref.watch(currentUserIdProvider);
  if (donorId == null) return const [];
  return ref.watch(appointmentsRepositoryProvider).myAppointments(donorId);
});

final bloodBanksProvider =
    FutureProvider.autoDispose<List<BloodBank>>((ref) async {
  return ref.watch(appointmentsRepositoryProvider).bloodBanks();
});
