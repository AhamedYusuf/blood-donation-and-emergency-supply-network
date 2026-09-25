class EligibilityResponse {
  const EligibilityResponse({
    required this.isEligible,
    this.reason,
    this.daysUntilEligible,
  });

  final bool isEligible;
  final String? reason;
  final int? daysUntilEligible;

  factory EligibilityResponse.fromJson(Map<String, dynamic> json) => EligibilityResponse(
        isEligible: json['isEligible'] as bool,
        reason: json['reason'] as String?,
        daysUntilEligible: (json['daysUntilEligible'] as num?)?.toInt(),
      );
}