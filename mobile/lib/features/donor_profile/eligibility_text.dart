import 'models/eligibility_response.dart';

/// Turns the eligibility rule engine's machine-readable reason
/// (e.g. `verified_by_admin = false`) into a sentence a donor understands.
String eligibilityExplanation(String? reason) {
  if (reason == null || reason.isEmpty) {
    return 'You can donate whenever you are ready.';
  }
  if (reason.startsWith('verified_by_admin')) {
    return 'An administrator still needs to verify your donor profile. '
        'You will be able to donate once it is approved.';
  }
  if (reason.startsWith('last_donation_date')) {
    return 'You donated recently. Donors wait 90 days between donations '
        'so the body can recover.';
  }
  if (reason.startsWith('age')) {
    return 'Donors need to be between 18 and 65 years old.';
  }
  if (reason.startsWith('medical_flags.recent_illness')) {
    return 'You reported a recent illness or infection. Update your health '
        'details in your donor profile once you have recovered.';
  }
  if (reason.startsWith('blood type') || reason.startsWith('unrecognized')) {
    return 'Your blood type could not be matched. Check the blood type in '
        'your donor profile.';
  }
  return reason;
}

/// Short, one-line status for list rows and summaries.
String eligibilitySummary(EligibilityResponse? eligibility) {
  if (eligibility == null) return 'Check when you can donate next';
  if (eligibility.isEligible) return 'You can donate now';
  final days = eligibility.daysUntilEligible;
  if (days != null && days > 0) {
    return days == 1 ? 'You can donate again tomorrow' : 'You can donate again in $days days';
  }
  if ((eligibility.reason ?? '').startsWith('verified_by_admin')) {
    return 'Waiting for profile verification';
  }
  return 'Not able to donate yet';
}
