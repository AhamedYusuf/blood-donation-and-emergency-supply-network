import 'package:blood_donation_network/features/blood_requests/blood_request.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('fromJson parses a complete request and converts coordinates', () {
    final request = BloodRequest.fromJson({
      'id': 'request-1',
      'requesterId': 'user-1',
      'organizationId': 'org-1',
      'bloodType': 'O+',
      'unitsRequested': 4,
      'urgency': 'urgent',
      'status': 'open',
      'hospitalName': 'Central Hospital',
      'latitude': 24,
      'longitude': 67.5,
      'notes': 'Emergency supply needed',
      'createdAt': '2026-09-25T10:00:00Z',
      'fulfilledAt': '2026-09-25T12:00:00Z',
      'closedAt': '2026-09-25T13:00:00Z',
    });

    expect(request.id, 'request-1');
    expect(request.bloodType, 'O+');
    expect(request.latitude, 24.0);
    expect(request.longitude, 67.5);
    expect(request.fulfilledAt, DateTime.parse('2026-09-25T12:00:00Z'));
    expect(request.closedAt, DateTime.parse('2026-09-25T13:00:00Z'));
  });

  test('fromJson keeps nullable completion dates null', () {
    final request = BloodRequest.fromJson({
      'id': 'request-2',
      'requesterId': 'user-1',
      'organizationId': 'org-1',
      'bloodType': 'A-',
      'unitsRequested': 2,
      'urgency': 'normal',
      'status': 'open',
      'hospitalName': 'North Hospital',
      'latitude': 24.1,
      'longitude': 67.6,
      'notes': '',
      'createdAt': '2026-09-25T10:00:00Z',
      'fulfilledAt': null,
      'closedAt': null,
    });

    expect(request.fulfilledAt, isNull);
    expect(request.closedAt, isNull);
  });
}
