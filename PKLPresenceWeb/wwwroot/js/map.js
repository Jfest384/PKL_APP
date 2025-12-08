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

    L.tileLayer('https://api.maptiler.com/maps/satellite/{z}/{x}/{y}.jpg?key=V2NKLc84EZrQT0sfWuu5').addTo(map);

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



window.renderCompanyMap = function (elementId, lat, lon, apiKey, dotnetHelper) {
    if (!window.L) return;
    if (window.companyMapInstance) {
        window.companyMapInstance.remove();
    }

    var map = L.map(elementId).setView([lat, lon], 16);
    window.presenceMapInstance = map;

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png").addTo(map);

    // marker draggable
    let marker = L.marker([lat, lon], { draggable: true }).addTo(map);

    // DOM elements (harus sesuai id di Blazor)
    const coordInput = document.getElementById("company-coord");
    const addressInput = document.getElementById("company-address");

    function setCoordInput(latVal, lonVal) {
        if (!coordInput) return;
        // format with 7 decimal places and dot as decimal
        coordInput.value = `${latVal.toFixed(7)}, ${lonVal.toFixed(7)}`;
    }

    async function updateAddress(latVal, lonVal) {
        if (!addressInput) return;
        try {
            const res = await fetch(`https://us1.locationiq.com/v1/reverse?key=${apiKey}&lat=${latVal}&lon=${lonVal}&format=json`);
            const j = await res.json();
            const addr = j.display_name || "Alamat tidak ditemukan";
            addressInput.value = addr;

            if (dotnetHelper && typeof dotnetHelper.invokeMethodAsync === "function") {
                try {
                    dotnetHelper.invokeMethodAsync("NotifyAddressFromJs", addr).catch(() => { /* ignore */ });
                } catch { /* ignore */ }
            }
        } catch {
            addressInput.value = "Gagal memuat alamat";
            if (dotnetHelper && typeof dotnetHelper.invokeMethodAsync === "function") {
                try {
                    dotnetHelper.invokeMethodAsync("NotifyAddressFromJs", addressInput.value).catch(() => { /* ignore */ });
                } catch { /* ignore */ }
            }
        }
    }

    // set awal
    setCoordInput(lat, lon);
    updateAddress(lat, lon);

    // map click -> pindah marker, update input, update address, panggil C#
    map.on("click", function (e) {
        const { lat: plat, lng: plng } = e.latlng;
        marker.setLatLng([plat, plng]);
        setCoordInput(plat, plng);
        updateAddress(plat, plng);

        if (dotnetHelper) {
            dotnetHelper.invokeMethodAsync("OnMapClicked", plat, plng);
        }
    });

    // marker drag end
    marker.on("dragend", function () {
        const p = marker.getLatLng();
        setCoordInput(p.lat, p.lng);
        updateAddress(p.lat, p.lng);

        if (dotnetHelper) {
            dotnetHelper.invokeMethodAsync("OnMapClicked", p.lat, p.lng);
        }
    });

    // handle manual change on coord input — gunakan assignment (replace previous handler)
    if (coordInput) {
        coordInput.onchange = function () {
            const raw = coordInput.value || "";
            // split on first comma (separator between lat & lon)
            const idx = raw.indexOf(',');
            if (idx < 0) return;

            let a = raw.substring(0, idx).trim();
            let b = raw.substring(idx + 1).trim();

            // kalau user pakai koma sebagai decimal, ubah jadi titik
            a = a.replace(',', '.');
            b = b.replace(',', '.');

            const latVal = parseFloat(a);
            const lonVal = parseFloat(b);

            if (!isNaN(latVal) && !isNaN(lonVal)) {
                marker.setLatLng([latVal, lonVal]);
                map.setView([latVal, lonVal], 15);
                setCoordInput(latVal, lonVal);
                updateAddress(latVal, lonVal);

                if (dotnetHelper) {
                    dotnetHelper.invokeMethodAsync("OnMapClicked", latVal, lonVal);
                }
            }
        };
    }
};