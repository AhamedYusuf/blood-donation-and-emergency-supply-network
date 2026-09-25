import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api_client.dart';
import '../../auth/auth_controller.dart';
import '../models/donor_profile.dart';
import '../models/eligibility_response.dart';
import '../repository/donor_repository.dart';

final donorRepositoryProvider = Provider<DonorRepository>(
  (ref) => DonorRepository(ref.watch(apiClientProvider)),
);

final donorProfileProvider = FutureProvider.autoDispose<DonorProfile?>((ref) async {
  final profileId = ref.watch(currentDonorProfileIdProvider);
  final token = ref.watch(authTokenProvider);
  if (profileId == null || token == null) return null;

  try {
    return await ref.read(donorRepositoryProvider).getById(profileId, token: token);
  } on ApiException catch (error) {
    if (error.statusCode == 404) return null;
    rethrow;
  }
});

final eligibilityProvider = FutureProvider.autoDispose<EligibilityResponse>((ref) async {
  final profile = await ref.watch(donorProfileProvider.future);
  final token = ref.watch(authTokenProvider);
  if (profile == null || token == null) {
    throw ApiException('Donor profile is not available');
  }
  return ref.read(donorRepositoryProvider).getEligibility(profile.id, token: token);
});