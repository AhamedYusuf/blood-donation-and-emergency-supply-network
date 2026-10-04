import 'package:blood_donation_network/features/notifications/blood_request_notification_router.dart';
import 'package:flutter_test/flutter_test.dart';

const _requestId = '04b6a987-1234-4abc-8def-1234567890ab';
const _requestData = {
  'type': 'new_blood_request',
  'bloodRequestId': _requestId,
};
const _requestRoute = '/blood-requests/$_requestId';

void main() {
  test('parses new blood request notification data into a route', () {
    expect(
      BloodRequestNotificationRouter.routeFromData(_requestData),
      _requestRoute,
    );
  });

  test('does not route unrelated notification types', () {
    expect(
      BloodRequestNotificationRouter.routeFromData({
        'type': 'appointment_reminder',
        'bloodRequestId': _requestId,
      }),
      isNull,
    );
  });

  test('ignores missing or invalid blood request IDs', () {
    expect(
      BloodRequestNotificationRouter.routeFromData({
        'type': 'new_blood_request',
      }),
      isNull,
    );
    expect(
      BloodRequestNotificationRouter.routeFromData({
        'type': 'new_blood_request',
        'bloodRequestId': '../profile',
      }),
      isNull,
    );
  });

  test('notification tap returns the details route when authenticated', () {
    final notificationRouter = BloodRequestNotificationRouter();

    expect(
      notificationRouter.handleTap(
        _requestData,
        isAuthenticated: true,
        messageId: 'message-1',
      ),
      _requestRoute,
    );
  });

  test('queues cold-start tap until authentication is ready', () {
    final notificationRouter = BloodRequestNotificationRouter();

    expect(
      notificationRouter.handleTap(
        _requestData,
        isAuthenticated: false,
        messageId: 'message-1',
      ),
      isNull,
    );
    expect(notificationRouter.takePendingRoute(), _requestRoute);
    expect(notificationRouter.takePendingRoute(), isNull);
  });

  test('ignores duplicate notification taps with the same message ID', () {
    final notificationRouter = BloodRequestNotificationRouter();

    expect(
      notificationRouter.handleTap(
        _requestData,
        isAuthenticated: true,
        messageId: 'message-1',
      ),
      _requestRoute,
    );
    expect(
      notificationRouter.handleTap(
        _requestData,
        isAuthenticated: true,
        messageId: 'message-1',
      ),
      isNull,
    );
  });
}
