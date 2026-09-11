/// Small date helpers — no intl dependency, English only, which is enough
/// for this app.
library;

const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const _weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

String dayNum(DateTime d) => d.day.toString();
String monthAbbr(DateTime d) => _months[d.month - 1];
String weekdayAbbr(DateTime d) => _weekdays[d.weekday - 1];

/// e.g. "8:30 AM"
String clockTime(DateTime d) {
  final h24 = d.hour;
  final period = h24 < 12 ? 'AM' : 'PM';
  var h = h24 % 12;
  if (h == 0) h = 12;
  return '$h:${d.minute.toString().padLeft(2, '0')} $period';
}

/// e.g. "Fri 11 Sep · 8:30 AM"
String longStamp(DateTime d) =>
    '${weekdayAbbr(d)} ${d.day} ${monthAbbr(d)} · ${clockTime(d)}';

/// Human relative time, past or future: "tomorrow", "in 3 days", "in 5h",
/// "2 weeks ago", "just now".
String relativeTime(DateTime d, {DateTime? now}) {
  now ??= DateTime.now();
  final diff = d.difference(now);
  final future = !diff.isNegative;
  final a = diff.abs();

  String phrase;
  if (a.inMinutes < 1) {
    phrase = 'just now';
  } else if (a.inMinutes < 60) {
    phrase = '${a.inMinutes}m';
  } else if (a.inHours < 24) {
    phrase = '${a.inHours}h';
  } else if (a.inDays == 1) {
    return future ? 'tomorrow' : 'yesterday';
  } else if (a.inDays < 7) {
    phrase = '${a.inDays} days';
  } else if (a.inDays < 30) {
    final w = (a.inDays / 7).round();
    phrase = '$w week${w == 1 ? '' : 's'}';
  } else {
    final m = (a.inDays / 30).round();
    phrase = '$m month${m == 1 ? '' : 's'}';
  }
  if (phrase == 'just now') return phrase;
  return future ? 'in $phrase' : '$phrase ago';
}
