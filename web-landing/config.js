// Konfiguracja strony dowózka.pl ustawiana przy wdrożeniu (plik obok index.html).
// appUrl — adres aplikacji klienta (web): HTTPS, KATALOG GŁÓWNY (sub)domeny, bez ścieżki — ten sam co „Adres aplikacji
// klienta" w panelu (Konfiguracja → Aplikacja klienta i kody QR).
// Kanoniczny adres pilotażu: "https://app.dowózka.pl" — wpisz go DOPIERO gdy https://app.dowózka.pl działa
// (DNS + certyfikat + aplikacja wgrana, patrz DEPLOY.md → S2). Puste = sekcja „Zamów online" pokazuje „wkrótce", bez QR.
window.DOWOZKA_CONFIG = {
  appUrl: ""
};
