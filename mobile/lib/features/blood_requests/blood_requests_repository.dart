import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:http/http.dart' as http;

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import '../donor_profile/providers/donor_profile_provider.dart';
import 'blood_request.dart';

class BloodRequestsRepository {
  BloodRequestsRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);

  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/requests
  ///
  /// Loads requests, applying the stored donor location for donor accounts.
  Future<List<BloodRequest>> getRequests() async {
    var path = '/api/requests';
    final auth = _ref.read(authControllerProvider);

    if (auth.role?.toLowerCase() == 'donor') {
      final donorProfile = await _ref.read(donorProfileProvider.future);
      final latitude = donorProfile?.latitude;
      final longitude = donorProfile?.longitude;

      if (latitude == null || longitude == null) {
        throw ApiException(
          'Your donor location is unavailable. Add latitude and longitude '
          'to your donor profile to view nearby blood requests.',
        );
      }

      path = Uri(
        path: path,
        queryParameters: {
          'nearLat': latitude.toString(),
          'nearLng': longitude.toString(),
          'radiusKm': '50',
        },
      ).toString();
    }

    final json = await _api.get(
      path,
      token: _token,
    );

    final list = (json as List)
        .cast<Map<String, dynamic>>();

    return list
        .map(BloodRequest.fromJson)
        .toList();
  }

  /// GET /api/requests/{id}
  ///
  /// Loads one blood request by ID.
  Future<BloodRequest> getRequestById(
    String id,
  ) async {
    final json = await _api.get(
      '/api/requests/$id',
      token: _token,
    );

    return BloodRequest.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// POST /api/requests
  ///
  /// Creates a new blood request.
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

    return BloodRequest.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// PUT /api/requests/{id}/status
  ///
  /// Updates the status of a blood request.
  Future<BloodRequest> updateStatus({
    required String id,
    required String status,
  }) async {
    final json = await _api.put(
      '/api/requests/$id/status',
      token: _token,
      body: {
        'status': status,
      },
    );

    return BloodRequest.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// POST /api/requests/{id}/close
  ///
  /// Closes/cancels a blood request.
  Future<BloodRequest> closeRequest(
    String id,
  ) async {
    final json = await _api.post(
      '/api/requests/$id/close',
      token: _token,
    );

    return BloodRequest.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// DELETE /api/requests/{id}
  ///
  /// Deletes a blood request.
  ///
  /// DELETE is handled directly here so we don't need to modify
  /// the shared core/api_client.dart file.
  Future<void> deleteRequest(
    String id,
  ) async {
    final uri = Uri.parse(
      '${_api.baseUrl}/api/requests/$id',
    );

    http.Response response;

    try {
      response = await http.delete(
        uri,
        headers: {
          'Content-Type': 'application/json',
          if (_token != null)
            'Authorization': 'Bearer $_token',
        },
      );
    } catch (error) {
      throw ApiException(
        'Network error: $error',
      );
    }

    if (response.statusCode >= 200 &&
        response.statusCode < 300) {
      return;
    }

    String message =
        'Could not delete the blood request.';

    if (response.body.isNotEmpty) {
      try {
        final decoded =
            jsonDecode(response.body);

        if (decoded is Map<String, dynamic>) {
          message = (
                decoded['message'] ??
                decoded['title'] ??
                decoded['detail']
              )
              ?.toString() ??
              message;
        }
      } catch (_) {
        message = response.body;
      }
    }

    throw ApiException(
      message,
      statusCode: response.statusCode,
    );
  }
}

/// Repository provider.
final bloodRequestsRepositoryProvider =
    Provider<BloodRequestsRepository>(
  (ref) {
    return BloodRequestsRepository(ref);
  },
);

/// Loads the blood request list.
///
/// Other screens can invalidate this provider after
/// create/update/close/delete so the list refreshes.
final bloodRequestsProvider =
    FutureProvider.autoDispose<
        List<BloodRequest>>(
  (ref) {
    return ref
        .watch(
          bloodRequestsRepositoryProvider,
        )
        .getRequests();
  },
);