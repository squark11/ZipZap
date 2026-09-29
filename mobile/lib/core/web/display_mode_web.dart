import 'dart:js_interop';
import 'dart:js_interop_unsafe';

import 'package:web/web.dart' as web;

/// Chrome/Android i nowsze przeglądarki: `display-mode: standalone`; Safari/iOS: `navigator.standalone`.
bool isRunningStandalone() {
  try {
    if (web.window.matchMedia('(display-mode: standalone)').matches) return true;
    final iosStandalone = (web.window.navigator as JSObject).getProperty<JSAny?>('standalone'.toJS);
    return iosStandalone?.dartify() == true;
  } catch (_) {
    return false;
  }
}
