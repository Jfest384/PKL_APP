window.getCurrentPosition = function () {
    return new Promise(function (resolve, reject) {
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(function (pos) {
                resolve(pos);
            }, function (err) {
                reject(err);
            });
        } else {
            reject("Geolocation not supported");
        }
    });
};

window.renderPresenceMapWithMarker = function (elementId, lat, lng, dotnetHelper) {
    if (!window.L) return;
    if (window.presenceMapInstance) {
        window.presenceMapInstance.remove();
    }
    var map = L.map(elementId).setView([lat, lng], 16);
    window.presenceMapInstance = map;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);

    // Marker tidak draggable
    var marker = L.marker([lat, lng], { draggable: false }).addTo(map);

    // Saat map diklik, buka Google Maps berdasarkan koordinat marker
    map.on('click', function () {
        if (!marker) return;

        var markerLatLng = marker.getLatLng();
        var url = `https://www.google.com/maps/search/?api=1&query=${markerLatLng.lat},${markerLatLng.lng}`;
        window.open(url, '_blank');
    });
};

window.triggerFileInput = function (inputId) {
    var input = document.getElementById(inputId);
    if (input) input.click();
};

window.resetInputFile = function (id) {
    var input = document.getElementById(id);
    if (input) input.value = "";
};