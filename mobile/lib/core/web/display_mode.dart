// Czy strona działa jako skrót z ekranu głównego (PWA „standalone"). Na platformach nie-web zawsze false.
export 'display_mode_stub.dart' if (dart.library.js_interop) 'display_mode_web.dart';
