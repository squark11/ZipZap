import 'package:geolocator/geolocator.dart';

/// Prosta para współrzędnych klienta.
class LatLng {
  final double lat;
  final double lng;
  const LatLng(this.lat, this.lng);
}

/// Ustala bieżącą lokalizację klienta (GPS/przeglądarka). Zwraca null, gdy usługa
/// wyłączona lub brak zgody — wołający degraduje się łagodnie (lista bez sortowania wg odległości).
class LocationService {
  Future<LatLng?> current() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return null;
      var perm = await Geolocator.checkPermission();
      if (perm == LocationPermission.denied) perm = await Geolocator.requestPermission();
      if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) return null;
      final pos = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(accuracy: LocationAccuracy.medium),
      );
      return LatLng(pos.latitude, pos.longitude);
    } catch (_) {
      return null;
    }
  }
}
