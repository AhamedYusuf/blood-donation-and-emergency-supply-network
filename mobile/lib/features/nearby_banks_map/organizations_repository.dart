import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/secure_storage.dart';
import 'organization.dart';

class OrganizationsRepository {
  OrganizationsRepository({
    required this.apiClient,
    required this.secureStorage,
  });

  final ApiClient apiClient;
  final SecureStorage secureStorage;

  Future<List<Organization>> getOrganizations() async {
    final session =
        await secureStorage.readSession();

    if (session == null) {
      throw ApiException(
        'Your session has expired. Please log in again.',
      );
    }

    final response = await apiClient.get(
      '/api/organizations',
      token: session.token,
    );

    if (response is! List) {
      throw ApiException(
        'Invalid organizations response from the server.',
      );
    }

    return response
        .whereType<Map>()
        .map(
          (json) => Organization.fromJson(
            Map<String, dynamic>.from(json),
          ),
        )
        .toList();
  }
}

final organizationsRepositoryProvider =
    Provider<OrganizationsRepository>((ref) {
  return OrganizationsRepository(
    apiClient: ref.read(apiClientProvider),
    secureStorage:
        ref.read(secureStorageProvider),
  );
});

final organizationsProvider =
    FutureProvider.autoDispose<List<Organization>>(
  (ref) {
    final repository =
        ref.read(
      organizationsRepositoryProvider,
    );

    return repository.getOrganizations();
  },
);