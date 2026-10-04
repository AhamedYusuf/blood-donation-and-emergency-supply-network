class BloodRequestNotificationRouter {
  static final RegExp _guidPattern = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
  );

  final Set<String> _handledMessageIds = <String>{};
  String? _pendingRoute;

  bool get hasPendingRoute => _pendingRoute != null;

  static String? routeFromData(Map<String, dynamic> data) {
    if (data['type'] != 'new_blood_request') return null;

    final requestId = data['bloodRequestId'];
    if (requestId is! String || !_guidPattern.hasMatch(requestId)) {
      return null;
    }

    return Uri(pathSegments: ['', 'blood-requests', requestId]).toString();
  }

  String? handleTap(
    Map<String, dynamic> data, {
    required bool isAuthenticated,
    String? messageId,
  }) {
    final route = routeFromData(data);
    if (route == null || !_rememberMessage(messageId)) return null;

    if (!isAuthenticated) {
      _pendingRoute = route;
      return null;
    }

    _pendingRoute = null;
    return route;
  }

  String? takePendingRoute() {
    final route = _pendingRoute;
    _pendingRoute = null;
    return route;
  }

  bool _rememberMessage(String? messageId) {
    if (messageId == null || messageId.isEmpty) return true;
    if (!_handledMessageIds.add(messageId)) return false;

    if (_handledMessageIds.length > 100) {
      _handledMessageIds.remove(_handledMessageIds.first);
    }

    return true;
  }
}
