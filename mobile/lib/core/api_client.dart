import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:http/http.dart' as http;

/// Base URL of the shared ASP.NET Core API.
///
/// Override at build/run time, e.g.
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5067
///
/// Defaults to the Android emulator loopback alias (10.0.2.2) on the
/// backend's dev port. On an iOS simulator use http://localhost:5067;
/// on a real device use your machine's LAN IP.
const String kApiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://10.0.2.2:5067',
);

/// Thrown for any non-success response or transport failure.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => 'ApiException($statusCode): $message';
}

/// Thin JSON HTTP wrapper. Stateless with respect to auth — the caller
/// passes a bearer token for protected endpoints.
class ApiClient {
  ApiClient({http.Client? httpClient, this.baseUrl = kApiBaseUrl})
      : _http = httpClient ?? http.Client();

  final http.Client _http;
  final String baseUrl;

  Future<dynamic> get(String path, {String? token}) =>
      _send('GET', path, token: token);

  Future<dynamic> post(String path, {Object? body, String? token}) =>
      _send('POST', path, body: body, token: token);

  Future<dynamic> put(String path, {Object? body, String? token}) =>
      _send('PUT', path, body: body, token: token);

  Future<dynamic> _send(
    String method,
    String path, {
    Object? body,
    String? token,
  }) async {
    final uri = Uri.parse('$baseUrl$path');
    final headers = <String, String>{
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };

    http.Response response;
    try {
      final request = http.Request(method, uri)..headers.addAll(headers);
      if (body != null) request.body = jsonEncode(body);
      final streamed = await _http.send(request);
      response = await http.Response.fromStream(streamed);
    } catch (e) {
      throw ApiException('Network error: $e');
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(response.body);
    }

    throw ApiException(
      _extractMessage(response.body) ?? 'Request failed',
      statusCode: response.statusCode,
    );
  }

  /// Pulls a human-readable message out of an ASP.NET Core ProblemDetails
  /// body, falling back to the raw body.
  String? _extractMessage(String rawBody) {
    if (rawBody.isEmpty) return null;
    try {
      final decoded = jsonDecode(rawBody);
      if (decoded is Map<String, dynamic>) {
        final errors = decoded['errors'];
        if (errors is Map && errors.isNotEmpty) {
          final first = errors.values.first;
          if (first is List && first.isNotEmpty) return first.first.toString();
        }
        return (decoded['title'] ?? decoded['detail'])?.toString();
      }
    } catch (_) {
      // not JSON
    }
    return rawBody;
  }
}

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient();
  ref.onDispose(() => client._http.close());
  return client;
});
