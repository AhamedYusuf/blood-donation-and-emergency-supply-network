import '../../../core/api_client.dart';
import '../models/donor_profile.dart';
import '../models/eligibility_response.dart';

class DonorRepository {
  DonorRepository(this._api);

  final ApiClient _api;

  Future<DonorProfile> register(Map<String, dynamic> body, {required String token}) async {
    final response = await _api.post('/api/donors/register', body: body, token: token);
    return DonorProfile.fromJson(response as Map<String, dynamic>);
  }

  Future<DonorProfile> getById(String id, {required String token}) async {
    final response = await _api.get('/api/donors/$id', token: token);
    return DonorProfile.fromJson(response as Map<String, dynamic>);
  }

  Future<DonorProfile> update(String id, Map<String, dynamic> body, {required String token}) async {
    final response = await _api.put('/api/donors/$id', body: body, token: token);
    return DonorProfile.fromJson(response as Map<String, dynamic>);
  }

  Future<EligibilityResponse> getEligibility(String id, {required String token}) async {
    final response = await _api.get('/api/donors/$id/eligibility', token: token);
    return EligibilityResponse.fromJson(response as Map<String, dynamic>);
  }
}