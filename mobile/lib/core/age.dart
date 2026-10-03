/// Whole-years age as of [now] (defaults to the real current time),
/// matching how the web app's DonorProfilePage.tsx computes an age
/// threshold — not yet had this year's birthday counts as one year
/// younger.
bool isAtLeastYearsOld(DateTime birthDate, int years, {DateTime? now}) {
  final today = now ?? DateTime.now();
  var age = today.year - birthDate.year;
  if (today.month < birthDate.month ||
      (today.month == birthDate.month && today.day < birthDate.day)) {
    age--;
  }
  return age >= years;
}
