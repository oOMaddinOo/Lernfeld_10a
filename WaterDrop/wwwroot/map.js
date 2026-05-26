
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

window.selectedMarkerInfo = null;
window.userLat = null;
window.userLon = null;
window.allElements = [];
window.lastSearchCenter = null;

window.initMap = function (lat, lon) {
    if (window.map) return;

    window.map = L.map('map', { zoomControl: true }).setView([lat, lon], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(window.map);

    for (const cat of Object.keys(window.categoryLayers)) {
        const color = window.categoryColors[cat] || '#3461C1';
        window.categoryLayers[cat] = L.markerClusterGroup({
            maxClusterRadius: 45,
            spiderfyOnMaxZoom: true,
            showCoverageOnHover: false,
            zoomToBoundsOnClick: true,
            iconCreateFunction: function (cluster) {
                const count = cluster.getChildCount();
                let size, fontSize;
                if (count < 10)       { size = 32; fontSize = 12; }
                else if (count < 50)  { size = 38; fontSize = 13; }
                else                  { size = 44; fontSize = 14; }
                return L.divIcon({
                    html: `<div style="
                        background:${color};
                        color:white;
                        width:${size}px;height:${size}px;
                        border-radius:50%;
                        display:flex;align-items:center;justify-content:center;
                        font-size:${fontSize}px;font-weight:600;
                        font-family:system-ui,-apple-system,sans-serif;
                        box-shadow:0 2px 6px rgba(0,0,0,0.3);
                        border:2.5px solid white;
                    ">${count}</div>`,
                    className: '',
                    iconSize: [size, size],
                    iconAnchor: [size / 2, size / 2]
                });
            }
        }).addTo(window.map);
    }

    window.map.on('moveend', function () {
        if (!window.lastSearchCenter || !window.dotNetRef) return;
        const center = window.map.getCenter();
        const dist = haversineDistance(
            window.lastSearchCenter.lat, window.lastSearchCenter.lng,
            center.lat, center.lng
        );
        if (dist > 2000) {
            window.dotNetRef.invokeMethodAsync('ShowSearchAreaButton', true);
        }
    });
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

function createSelectedIcon() {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="38" viewBox="0 0 28 38">
        <path d="M14 0 C6.3 0 0 6.3 0 14 C0 24.5 14 38 14 38 C14 38 28 24.5 28 14 C28 6.3 21.7 0 14 0 Z" fill="#EA4335"/>
        <circle cx="14" cy="14" r="6" fill="white" opacity="0.4"/>
    </svg>`;
    return L.divIcon({
        html: svg,
        className: '',
        iconSize: [28, 38],
        iconAnchor: [14, 38],
        popupAnchor: [0, -38]
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

function getFacilityName(el) {
    const tags = el.tags || {};
    if (tags.name) return tags.name;

    // Build a descriptive name from the amenity type + features
    let label;
    if (tags.amenity === 'drinking_water') {
        label = 'Drinking Fountain';
    } else {
        const parts = [];
        if (tags.wheelchair === 'yes') parts.push('Accessible');
        parts.push('Restroom');
        if (tags.changing_table === 'yes') parts.push('with Changing Table');
        label = parts.join(' ');
    }

    // Append street/area if available for context
    const street = tags['addr:street'];
    const city = tags['addr:city'];
    if (street) return label + ' — ' + street;
    if (city) return label + ' — ' + city;

    return label;
}

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

function isOpenNow(ohStr) {
    if (!ohStr) return null;
    if (ohStr === '24/7') return true;
    try {
        const now = new Date();
        const days = ['Su','Mo','Tu','We','Th','Fr','Sa'];
        const day = days[now.getDay()];
        const mins = now.getHours() * 60 + now.getMinutes();
        const ranges = ohStr.split(';').map(s => s.trim());
        for (const range of ranges) {
            const match = range.match(/^([A-Za-z, -]+)\s+(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})$/);
            if (!match) continue;
            const dayPart = match[1];
            const open = parseInt(match[2]) * 60 + parseInt(match[3]);
            const close = parseInt(match[4]) * 60 + parseInt(match[5]);
            const dayRange = dayPart.split(',').map(d => d.trim());
            for (const dr of dayRange) {
                if (dr.includes('-')) {
                    const [start, end] = dr.split('-').map(d => d.trim());
                    const si = days.indexOf(start), ei = days.indexOf(end);
                    const di = days.indexOf(day);
                    if (si >= 0 && ei >= 0 && di >= 0) {
                        const inRange = si <= ei ? (di >= si && di <= ei) : (di >= si || di <= ei);
                        if (inRange && mins >= open && mins < close) return true;
                    }
                } else if (dr === day) {
                    if (mins >= open && mins < close) return true;
                }
            }
        }
        return false;
    } catch (e) {
        return null;
    }
}

function buildPopupContent(tags) {
    const s = 'font-family:system-ui,-apple-system,sans-serif;min-width:140px;';
    let html = '';

    // Facility type with colored dot
    const amenity = tags.amenity || 'toilets';
    const isDw = amenity === 'drinking_water';
    const typeLabel = isDw ? 'Drinking Fountain' : 'Public Restroom';
    const typeColor = isDw ? '#4E9FD4' : '#3461C1';
    html += `<div style="display:flex;align-items:center;gap:5px;margin-bottom:4px;">` +
            `<span style="width:7px;height:7px;border-radius:50%;background:${typeColor};display:inline-block;flex-shrink:0;"></span>` +
            `<span style="font-size:10px;color:#888;text-transform:uppercase;letter-spacing:0.04em;font-weight:600;">${typeLabel}</span>` +
            `</div>`;

    // Name
    if (tags.name) {
        html += `<strong style="font-size:13px;display:block;margin-bottom:3px;">${tags.name}</strong>`;
    }

    // Address
    const street = tags['addr:street'];
    const num = tags['addr:housenumber'];
    const city = tags['addr:city'];
    if (street || city) {
        let addr = '';
        if (street) addr += street;
        if (num) addr += ' ' + num;
        if (city) addr += (addr ? ', ' : '') + city;
        html += `<div style="color:#666;font-size:12px;margin-bottom:3px;">${addr}</div>`;
    }

    // Badges row
    const badges = [];

    // Open / Closed
    const openStatus = isOpenNow(tags.opening_hours);
    if (openStatus === true) {
        badges.push(`<span style="background:rgba(91,168,90,0.14);color:#3d7a3c;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">Open</span>`);
    } else if (openStatus === false) {
        badges.push(`<span style="background:rgba(220,60,60,0.12);color:#b33;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">Closed</span>`);
    }

    // Opening hours text (always show if present)
    if (tags.opening_hours) {
        html += `<div style="color:#666;font-size:11px;margin-bottom:3px;">&#128336; ${tags.opening_hours}</div>`;
    }

    // Fee
    if (tags.fee === 'yes') {
        badges.push(`<span style="background:rgba(232,160,48,0.14);color:#9a6a1a;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">Paid</span>`);
    } else if (tags.fee === 'no') {
        badges.push(`<span style="background:rgba(91,168,90,0.14);color:#3d7a3c;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">Free</span>`);
    }

    // Wheelchair
    if (tags.wheelchair === 'yes') {
        badges.push(`<span style="background:rgba(91,168,90,0.14);color:#3d7a3c;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">&#9855; Accessible</span>`);
    }

    // Changing table
    if (tags.changing_table === 'yes') {
        badges.push(`<span style="background:rgba(232,160,48,0.14);color:#9a6a1a;padding:2px 6px;border-radius:6px;font-size:10px;font-weight:600;">Changing Table</span>`);
    }

    if (badges.length > 0) {
        html += `<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:2px;">${badges.join('')}</div>`;
    }

    return `<div style="${s}">${html}</div>`;
}

window.addCategoryElements = async function (elements, activeCategories) {
    if (!window.map) {
        console.error('Map not initialized!');
        return {};
    }

    window.markersByElementId = {};
    window.selectedMarkerInfo = null;
    window.allElements = elements;

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
                // Restore previously selected marker to its original icon
                if (window.selectedMarkerInfo) {
                    try {
                        window.selectedMarkerInfo.marker.setIcon(window.selectedMarkerInfo.originalIcon);
                    } catch (e) {}
                }

                // Highlight this marker with the red selected icon
                window.selectedMarkerInfo = { marker: marker, originalIcon: icon };
                marker.setIcon(createSelectedIcon());

                const name = getFacilityName(el);
                if (window.dotNetRef) {
                    window.dotNetRef.invokeMethodAsync(
                        'OnMarkerSelected',
                        String(el.id),
                        name,
                        el.lat,
                        el.lon,
                        el.tags || {}
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

    window.lastSearchCenter = window.map.getCenter();

    return counts;
};

window.getMapBounds = function () {
    if (!window.map) return null;
    const b = window.map.getBounds();
    return {
        minLat: b.getSouth(),
        maxLat: b.getNorth(),
        minLon: b.getWest(),
        maxLon: b.getEast()
    };
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

window.fitMapToBounds = function (minLat, minLon, maxLat, maxLon) {
    if (!window.map) return;
    window.map.fitBounds([[minLat, minLon], [maxLat, maxLon]], { padding: [20, 20] });
};

function haversineDistance(lat1, lon1, lat2, lon2) {
    const R = 6371000;
    const toRad = d => d * Math.PI / 180;
    const dLat = toRad(lat2 - lat1);
    const dLon = toRad(lon2 - lon1);
    const a = Math.sin(dLat / 2) ** 2 +
              Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

window.getDistanceFromUser = function (lat, lon) {
    if (window.userLat == null || window.userLon == null) return -1;
    return haversineDistance(window.userLat, window.userLon, lat, lon);
};

window.getNearbyFacilities = function (limit) {
    if (window.userLat == null || window.userLon == null) return [];
    if (!window.allElements || window.allElements.length === 0) return [];

    return window.allElements
        .filter(el => el.lat && el.lon)
        .map(el => ({
            id: el.id,
            name: getFacilityName(el),
            lat: el.lat,
            lon: el.lon,
            distance: haversineDistance(window.userLat, window.userLon, el.lat, el.lon),
            amenity: (el.tags && el.tags.amenity) || 'toilets'
        }))
        .sort((a, b) => a.distance - b.distance)
        .slice(0, limit || 8);
};

window.selectFacilityById = function (elementId) {
    if (!window.map) return;
    const markers = window.markersByElementId && window.markersByElementId[elementId];
    if (!markers || markers.length === 0) return;
    const marker = markers[0];
    window.map.setView(marker.getLatLng(), 16);
    marker.fire('click');
};

// "Du bist hier"-Marker: ein erkennbar anderer Stil als die Toiletten-Marker,
// damit Nutzer sofort sehen, wo sie sich befinden. Wir merken uns die
// Referenz auf window, damit wiederholte Aufrufe den alten Marker ersetzen
// statt zu duplizieren.
function setUserLocationMarker(lat, lon, accuracy) {
    if (!window.map) return;

    window.userLat = lat;
    window.userLon = lon;

    // Remove previous marker and accuracy circle
    if (window.userLocationMarker) {
        try { window.map.removeLayer(window.userLocationMarker); } catch (e) {}
    }
    if (window.userAccuracyCircle) {
        try { window.map.removeLayer(window.userAccuracyCircle); } catch (e) {}
    }

    // Pulsing blue dot (Google Maps style)
    const icon = L.divIcon({
        html: '<div class="wd-user-loc"><div class="wd-user-loc-pulse"></div><div class="wd-user-loc-dot"></div></div>',
        className: '',
        iconSize: [18, 18],
        iconAnchor: [9, 9]
    });

    window.userLocationMarker = L.marker([lat, lon], { icon, zIndexOffset: 1000, interactive: false })
        .addTo(window.map);

    // Accuracy circle (translucent blue)
    if (accuracy && accuracy > 0) {
        window.userAccuracyCircle = L.circle([lat, lon], {
            radius: accuracy,
            color: '#4285F4',
            weight: 1,
            opacity: 0.3,
            fillColor: '#4285F4',
            fillOpacity: 0.1,
            interactive: false
        }).addTo(window.map);
    }
}

window.initMapWithGeolocate = function (dotNetRef) {
    window.dotNetRef = dotNetRef;
    window.initMap(51.0, 10.0);
    window.map.setZoom(6);

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
            setUserLocationMarker(lat, lon, pos.coords.accuracy);
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
            dotNetRef.invokeMethodAsync('SetGeolocationStatus', err.code === 1 ? 'denied' : 'unavailable');
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
        setUserLocationMarker(lat, lon, pos.coords.accuracy);
    });
};


function buildPopupWithReviewAndPicture(baseContent, reviewText, pictureUrl) {
    let addition = '';
    if (reviewText) {
        addition += `<div style="font-size:12px;color:#444;margin-top:4px;"><strong>Review:</strong> ${reviewText}</div>`;
    }
    if (pictureUrl) {
        addition += `<img src="${pictureUrl}" alt="Photo" style="max-width:200px;max-height:120px;margin-top:6px;border-radius:6px;display:block;object-fit:cover;">`;
    }
    if (!addition) return baseContent;
    return baseContent + `<div style="margin-top:6px;padding-top:6px;border-top:1px solid #eee;">${addition}</div>`;
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

window.clearSelectedMarker = function () {
    if (window.selectedMarkerInfo) {
        try {
            window.selectedMarkerInfo.marker.setIcon(window.selectedMarkerInfo.originalIcon);
        } catch (e) {}
        window.selectedMarkerInfo = null;
    }
};
