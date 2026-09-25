import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'organization.dart';

class OrganizationsRepository {
  OrganizationsRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/organizations.
  Future<List<Organization>> getOrganizations() async {
    final json = await _api.get('/api/organizations', token: _token);
    final list = (json as List<dynamic>).whereType<Map<String, dynamic>>();
    return list.map(Organization.fromJson).toList();
  }

  /// GET /api/organizations/{id}.
  Future<Organization> getOrganization(String id) async {
    final json = await _api.get('/api/organizations/$id', token: _token);
    return Organization.fromJson(json as Map<String, dynamic>);
  }
}

final organizationsRepositoryProvider = Provider<OrganizationsRepository>(
  (ref) => OrganizationsRepository(ref),
);

final bloodRequestOrganizationsProvider =
    FutureProvider.autoDispose<List<Organization>>((ref) {
      return ref.watch(organizationsRepositoryProvider).getOrganizations();
    });
