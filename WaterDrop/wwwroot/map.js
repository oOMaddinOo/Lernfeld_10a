
window.map = null;

window.categoryLayers = {
    restrooms: null,
    drinking_water: null,
    accessible: null,
    changing_table: null
};

window.categoryColors = {
    restrooms: '#3461C1',
    drinking_water: '#4E9FD4',
    accessible: '#5BA85A',
    changing_table: '#E8A030'
};

window.initMap = function (lat, lon) {
    if (window.map) return;

    window.map = L.map('map', { zoomControl: true }).setView([lat, lon], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(window.map);

    for (const cat of Object.keys(window.categoryLayers)) {
        window.categoryLayers[cat] = L.layerGroup().addTo(window.map);
    }
};

function createTeardropIcon(color) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="22" height="30" viewBox="0 0 22 30">
        <path d="M11 0 C4.9 0 0 4.9 0 11 C0 19.3 11 30 11 30 C11 30 22 19.3 22 11 C22 4.9 17.1 0 11 0 Z" fill="${color}"/>
        <circle cx="11" cy="11" r="5" fill="white" opacity="0.35"/>
    </svg>`;
    return L.divIcon({
        html: svg,
        className: '',
        iconSize: [22, 30],
        iconAnchor: [11, 30],
        popupAnchor: [0, -30]
    });
}

// HINWEIS: Die früheren Helfer buildOverpassQuery / buildCombinedOverpassQuery
// und die Funktionen window.fetchAllCategories / window.fetchCategory wurden
// entfernt. Toilettendaten werden jetzt vollständig aus der ToiletData-Tabelle
// geladen — der Datenfluss ist:
//     kloService.GetToiletsByCity (C#, liest aus DB)
//   → JS addCategoryElements(elements, activeCategories)
//   → Marker auf der Karte
// Es gibt keinen direkten Overpass-Aufruf mehr im Frontend.

function classifyElement(el) {
    const tags = el.tags || {};
    const categories = [];
    if (tags.amenity === 'drinking_water') {
        categories.push('drinking_water');
    } else if (tags.amenity === 'toilets') {
        categories.push('restrooms');
        if (tags.wheelchair === 'yes') categories.push('accessible');
        if (tags.changing_table === 'yes') categories.push('changing_table');
    }
    return categories;
}

function buildPopupContent(tags) {
    let html = '';
    if (tags.name) html += `<strong style="font-size:13px;">${tags.name}</strong><br>`;

    const street = tags['addr:street'];
    const num = tags['addr:housenumber'];
    const city = tags['addr:city'];
    if (street || city) {
        let addr = '';
        if (street) addr += street;
        if (num) addr += ' ' + num;
        if (city) addr += (addr ? ', ' : '') + city;
        html += `<span style="color:#666;font-size:12px;">${addr}</span><br>`;
    }

    if (tags.opening_hours) {
        html += `<span style="color:#666;font-size:12px;">&#128336; ${tags.opening_hours}</span><br>`;
    }

    if (!html) html = '<span style="color:#999;font-size:12px;">No details available</span>';
    return `<div style="font-family:system-ui,-apple-system,sans-serif;min-width:120px;">${html}</div>`;
}

window.addCategoryElements = async function (elements, activeCategories) {
    if (!window.map) {
        console.error('Map not initialized!');
        return {};
    }

    window.markersByElementId = {};

    const counts = {};
    for (const cat of activeCategories) counts[cat] = 0;

    // Wenn die Karte bereits auf den Standort des Nutzers zentriert ist
    // (User-Marker wurde nach erfolgreicher Geolokation gesetzt), NICHT
    // automatisch auf den ersten Toiletten-Marker umzentrieren — sonst
    // springt die Ansicht vom Nutzer weg auf z. B. die Stadtmitte.
    let centered = !!window.userLocationMarker;

    for (const el of elements) {
        const cats = classifyElement(el).filter(c => activeCategories.includes(c));
        if (cats.length === 0) continue;

        const popup = buildPopupContent(el.tags || {});
        for (const cat of cats) {
            const icon = createTeardropIcon(window.categoryColors[cat]);
            const marker = L.marker([el.lat, el.lon], { icon });
            marker._basePopupContent = popup;
            marker.bindPopup(popup).addTo(window.categoryLayers[cat]);
            counts[cat] = (counts[cat] || 0) + 1;

            if (!window.markersByElementId[el.id]) window.markersByElementId[el.id] = [];
            window.markersByElementId[el.id].push(marker);

            marker.on('click', function () {
                const name = (el.tags && el.tags.name) ? el.tags.name : ('ID: ' + el.id);
                if (window.dotNetRef) {
                    // Lat/Lon mitgeben, damit der Wegbeschreibungs-Button im
                    // Review-Panel direkt zu Google Maps verlinken kann.
                    window.dotNetRef.invokeMethodAsync(
                        'OnMarkerSelected',
                        String(el.id),
                        name,
                        el.lat,
                        el.lon
                    );
                }
            });
        }

        if (!centered) {
            window.map.setView([el.lat, el.lon], 13);
            centered = true;
        }
    }

    const elementIds = Object.keys(window.markersByElementId);
    if (elementIds.length > 0 && window.dotNetRef) {
        try {
            const reviews = await window.dotNetRef.invokeMethodAsync('GetReviewsForElements', elementIds);
            for (const [idStr, info] of Object.entries(reviews)) {
                const markers = window.markersByElementId[idStr] || [];
                for (const m of markers) {
                    m.setPopupContent(buildPopupWithReviewAndPicture(m._basePopupContent, info.comment, info.pictureUrl));
                }
            }
        } catch (e) {
            console.warn('Could not load reviews:', e);
        }
    }

    return counts;
};

window.clearCategoryLayer = function (category) {
    if (window.categoryLayers[category]) {
        window.categoryLayers[category].clearLayers();
    }
};

window.hideCategoryLayer = function (category) {
    if (window.map && window.categoryLayers[category]) {
        window.map.removeLayer(window.categoryLayers[category]);
    }
};

window.showCategoryLayer = function (category) {
    if (window.map && window.categoryLayers[category]) {
        window.categoryLayers[category].addTo(window.map);
    }
};

window.categoryHasData = function (category) {
    const layer = window.categoryLayers[category];
    return layer ? layer.getLayers().length > 0 : false;
};

window.centerMap = function (lat, lon, zoom = 13) {
    if (window.map) {
        window.map.setView([lat, lon], zoom);
    }
};

// "Du bist hier"-Marker: ein erkennbar anderer Stil als die Toiletten-Marker,
// damit Nutzer sofort sehen, wo sie sich befinden. Wir merken uns die
// Referenz auf window, damit wiederholte Aufrufe den alten Marker ersetzen
// statt zu duplizieren.
function setUserLocationMarker(lat, lon) {
    if (!window.map) return;
    if (window.userLocationMarker) {
        try { window.map.removeLayer(window.userLocationMarker); } catch (e) { /* ignore */ }
    }
    const icon = L.divIcon({
        html: `<div style="
            width: 16px; height: 16px;
            background: #1a73e8;
            border: 3px solid white;
            border-radius: 50%;
            box-shadow: 0 0 0 2px rgba(26, 115, 232, 0.4), 0 2px 4px rgba(0,0,0,0.3);
        "></div>`,
        className: '',
        iconSize: [16, 16],
        iconAnchor: [8, 8]
    });
    window.userLocationMarker = L.marker([lat, lon], { icon, zIndexOffset: 1000 })
        .bindPopup('📍 Du bist hier')
        .addTo(window.map);
}

window.initMapWithGeolocate = function (dotNetRef) {
    window.dotNetRef = dotNetRef;
    window.initMap(51.0, 10.0);
    window.map.setZoom(6);

    // Loading-Status an Blazor melden, damit das UI eine sichtbare Rückmeldung
    // zeigt, während die Geolokation läuft (sonst sieht es einfach "tot" aus).
    dotNetRef.invokeMethodAsync('SetGeolocationStatus', 'locating');

    if (!navigator.geolocation) {
        dotNetRef.invokeMethodAsync('SetGeolocationStatus', 'unavailable');
        dotNetRef.invokeMethodAsync('SetCityAndSearch', 'Hamburg');
        return;
    }

    navigator.geolocation.getCurrentPosition(
        async function (pos) {
            const lat = pos.coords.latitude;
            const lon = pos.coords.longitude;
            window.map.setView([lat, lon], 13);
            setUserLocationMarker(lat, lon);
            dotNetRef.invokeMethodAsync('SetGeolocationStatus', 'loading_toilets');
            try {
                const resp = await fetch(
                    `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json`,
                    { headers: { 'Accept-Language': 'en' } }
                );
                const geo = await resp.json();
                const addr = geo.address || {};
                const city = addr.city || addr.town || addr.village || addr.county || 'Hamburg';
                dotNetRef.invokeMethodAsync('SetCityAndSearch', city);
            } catch (e) {
                dotNetRef.invokeMethodAsync('SetCityAndSearch', 'Hamburg');
            }
        },
        function (err) {
            console.warn('Geolocation failed:', err.message);
            window.map.setView([53.5801097, 9.8859876], 13);
            dotNetRef.invokeMethodAsync('SetGeolocationStatus', 'denied');
            dotNetRef.invokeMethodAsync('SetCityAndSearch', 'Hamburg');
        },
        { timeout: 8000, maximumAge: 300000 }
    );
};

window.locateUser = function () {
    if (!navigator.geolocation || !window.map) return;
    navigator.geolocation.getCurrentPosition(function (pos) {
        const lat = pos.coords.latitude;
        const lon = pos.coords.longitude;
        window.map.setView([lat, lon], 15);
        // Auch beim manuellen "Locate"-Klick den User-Marker aktualisieren.
        setUserLocationMarker(lat, lon);
    });
};


function buildPopupWithReviewAndPicture(baseContent, reviewText, pictureUrl) {
    const noDetailsSpan = '<span style="color:#999;font-size:12px;">No details available</span>';
    let addition = '';
    if (reviewText) {
        addition += `<div style="font-size:12px;color:#444;"><strong>Review:</strong> ${reviewText}</div>`;
    }
    if (pictureUrl) {
        addition += `<img src="${pictureUrl}" alt="Toilet" style="max-width:200px;max-height:150px;margin-top:8px;border-radius:5px;display:block;">`;
    }
    if (!addition) return baseContent;
    if (baseContent.includes(noDetailsSpan)) {
        return baseContent.replace(noDetailsSpan, addition);
    }
    return baseContent + `<div style="margin-top:8px;padding-top:8px;border-top:1px solid #eee;">${addition}</div>`;
}

function buildPopupWithReview(baseContent, reviewText) {
    return buildPopupWithReviewAndPicture(baseContent, reviewText, null);
}

window.updateMarkersWithReview = function (elementIdStr, reviewText) {
    // Behalten für Rückwärtskompatibilität — verwendet keine Bild-URL.
    const markers = window.markersByElementId && window.markersByElementId[elementIdStr];
    if (!markers) return;
    for (const m of markers) {
        m.setPopupContent(buildPopupWithReview(m._basePopupContent, reviewText));
    }
};

// Neuer Helper: aktualisiert das Popup eines Markers mit Kommentar UND Bild.
// Wird nach Add/Edit/Delete aus Blazor aufgerufen, damit das Popup immer den
// neuesten Review zeigt.
window.updateMarkersWithReviewAndPicture = function (elementIdStr, reviewText, pictureUrl) {
    const markers = window.markersByElementId && window.markersByElementId[elementIdStr];
    if (!markers) return;
    for (const m of markers) {
        m.setPopupContent(buildPopupWithReviewAndPicture(m._basePopupContent, reviewText, pictureUrl));
    }
};
