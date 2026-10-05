import 'package:blood_donation_network/app/mobile_shell.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('MobileShell route-to-tab mapping', () {
    test('blood requests route selects Requests at index 2', () {
      expect(
        MobileShell.selectedIndexForLocation(
          '/blood-requests',
          fallbackIndex: 0,
        ),
        2,
      );
    });

    test('blood request details route selects Requests at index 2', () {
      expect(
        MobileShell.selectedIndexForLocation(
          '/blood-requests/request-id',
          fallbackIndex: 0,
        ),
        2,
      );
    });

    test('blood request workflow route selects Requests at index 2', () {
      expect(
        MobileShell.selectedIndexForLocation(
          '/blood-requests/request-id/workflow',
          fallbackIndex: 0,
        ),
        2,
      );
    });

    test('profile route selects Profile at index 3', () {
      expect(
        MobileShell.selectedIndexForLocation('/profile', fallbackIndex: 0),
        3,
      );
    });

    test('staff have no Donations tab: Requests is index 1, Profile index 2', () {
      expect(
        MobileShell.selectedIndexForLocation('/blood-requests', fallbackIndex: 0, isStaff: true),
        1,
      );
      expect(
        MobileShell.selectedIndexForLocation('/profile', fallbackIndex: 0, isStaff: true),
        2,
      );
      expect(
        MobileShell.selectedIndexForLocation('/appointments', fallbackIndex: 0, isStaff: true),
        0,
      );
    });
  });
}
