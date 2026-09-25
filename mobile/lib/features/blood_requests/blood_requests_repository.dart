import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'blood_request.dart';

class BloodRequestsRepository {
  BloodRequestsRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/requests.
  Future<List<BloodRequest>> getRequests() async {
    final json = await _api.get('/api/requests', token: _token);
    final list = (json as List).cast<Map<String, dynamic>>();
    return list.map(BloodRequest.fromJson).toList();
  }

  /// GET /api/requests/{id}.
  Future<BloodRequest> getRequestById(String id) async {
    final json = await _api.get('/api/requests/$id', token: _token);
    return BloodRequest.fromJson(json as Map<String, dynamic>);
  }

  /// POST /api/requests.
  Future<BloodRequest> createRequest({
    required String organizationId,
    required String bloodType,
    required int unitsRequested,
    required String urgency,
    required String hospitalName,
    required double latitude,
    required double longitude,
    String notes = '',
  }) async {
    final json = await _api.post(
      '/api/requests',
      token: _token,
      body: {
        'organizationId': organizationId,
        'bloodType': bloodType,
        'unitsRequested': unitsRequested,
        'urgency': urgency,
        'hospitalName': hospitalName,
        'latitude': latitude,
        'longitude': longitude,
        'notes': notes,
      },
    );
    return BloodRequest.fromJson(json as Map<String, dynamic>);
  }
}

final bloodRequestsRepositoryProvider =
    Provider<BloodRequestsRepository>((ref) => BloodRequestsRepository(ref));