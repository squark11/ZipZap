{{flutter_js}}
{{flutter_build_config}}

// Własny start aplikacji: ekran ładowania z index.html znika dopiero, gdy aplikacja się uruchomi
// (bez pustej białej strony między skanem kodu QR a pierwszym ekranem).
_flutter.loader.load({
  onEntrypointLoaded: async function (engineInitializer) {
    const appRunner = await engineInitializer.initializeEngine();
    await appRunner.runApp();
    const loading = document.getElementById('app-loading');
    if (loading) loading.remove();
  },
});
