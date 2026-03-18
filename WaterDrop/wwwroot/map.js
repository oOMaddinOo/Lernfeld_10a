// ===== NEW: Category-based map functionality =====

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

function buildOverpassQuery(category, city) {
    const escaped = city.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    let tagFilter = '';
    switch (category) {
        case 'restrooms':       tagFilter = '[amenity=toilets]'; break;
        case 'drinking_water':  tagFilter = '[amenity=drinking_water]'; break;
        case 'accessible':      tagFilter = '[amenity=toilets][wheelchair=yes]'; break;
        case 'changing_table':  tagFilter = '[amenity=toilets][changing_table=yes]'; break;
    }
    return `[out:json][timeout:25];area[name="${escaped}"]->.searchArea;(node${tagFilter}(area.searchArea);way${tagFilter}(area.searchArea););out body;>;out skel qt;`;
}

function buildCombinedOverpassQuery(city) {
    const escaped = city.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    return `[out:json][timeout:60];area[name="${escaped}"]->.searchArea;(node[amenity=toilets](area.searchArea);way[amenity=toilets](area.searchArea);node[amenity=drinking_water](area.searchArea);way[amenity=drinking_water](area.searchArea););out body;>;out skel qt;`;
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

window.fetchAllCategories = async function (city, activeCategories) {
    if (!window.map) {
        console.error('Map not initialized!');
        return {};
    }

    for (const cat of activeCategories) {
        if (window.categoryLayers[cat]) {
            window.categoryLayers[cat].clearLayers();
        }
    }

    const query = buildCombinedOverpassQuery(city);
    const response = await fetch('https://overpass-api.de/api/interpreter', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'data=' + encodeURIComponent(query)
    });

    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const data = await response.json();
    const elements = (data.elements || []).filter(el => el.lat != null && el.lon != null);

    const counts = {};
    for (const cat of activeCategories) counts[cat] = 0;

    let centered = false;
    for (const el of elements) {
        const cats = classifyElement(el).filter(c => activeCategories.includes(c));
        if (cats.length === 0) continue;

        const popup = buildPopupContent(el.tags || {});
        for (const cat of cats) {
            const icon = createTeardropIcon(window.categoryColors[cat]);
            L.marker([el.lat, el.lon], { icon })
                .bindPopup(popup)
                .addTo(window.categoryLayers[cat]);
            counts[cat] = (counts[cat] || 0) + 1;
        }

        if (!centered) {
            window.map.setView([el.lat, el.lon], 13);
            centered = true;
        }
    }

    return counts;
};

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

window.fetchCategory = async function (category, city) {
    if (!window.map) {
        console.error('Map not initialized!');
        return 0;
    }

    if (window.categoryLayers[category]) {
        window.categoryLayers[category].clearLayers();
    }

    const query = buildOverpassQuery(category, city);
    const icon = createTeardropIcon(window.categoryColors[category]);

    const response = await fetch('https://overpass-api.de/api/interpreter', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'data=' + encodeURIComponent(query)
    });

    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const data = await response.json();
    const elements = (data.elements || []).filter(el => el.lat != null && el.lon != null);

    let count = 0;
    let centered = false;
    for (const el of elements) {
        const popup = buildPopupContent(el.tags || {});
        L.marker([el.lat, el.lon], { icon })
            .bindPopup(popup)
            .addTo(window.categoryLayers[category]);
        if (!centered) {
            window.map.setView([el.lat, el.lon], 13);
            centered = true;
        }
        count++;
    }

    return count;
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

window.initMapWithGeolocate = function (dotNetRef) {
    window.initMap(51.0, 10.0);
    window.map.setZoom(6);

    if (!navigator.geolocation) {
        dotNetRef.invokeMethodAsync('SetCityAndSearch', 'Hamburg');
        return;
    }

    navigator.geolocation.getCurrentPosition(
        async function (pos) {
            const lat = pos.coords.latitude;
            const lon = pos.coords.longitude;
            window.map.setView([lat, lon], 13);
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
            dotNetRef.invokeMethodAsync('SetCityAndSearch', 'Hamburg');
        },
        { timeout: 8000, maximumAge: 300000 }
    );
};

window.locateUser = function () {
    if (!navigator.geolocation || !window.map) return;
    navigator.geolocation.getCurrentPosition(function (pos) {
        window.map.setView([pos.coords.latitude, pos.coords.longitude], 15);
    });
};


// ===== COMMENTED OUT OLD FUNCTIONS =====
/*
window.markers = [];

window.addMarker = function (elementId, lat, lon, type, tags, comment, pictureUrl) {

    if (!window.map) {
        console.error("Map not initialized!");
        return;
    }

    let tagsHtml = '';
    if (tags && typeof tags === 'object' && Object.keys(tags).length > 0) {
        tagsHtml = '<strong>Tags:</strong><ul style="margin: 5px 0; padding-left: 20px;">';
        for (const [key, value] of Object.entries(tags)) {
            tagsHtml += `<li><strong>${key}:</strong> ${value}</li>`;
        }
        tagsHtml += '</ul>';
    } else {
        tagsHtml = '<em>Keine Tags verfügbar</em>';
    }

    let elementIdHtml = '';
    if (elementId) {
        elementIdHtml = `<strong>Id:</strong> ${elementId}<br>`;
    }

    let commentHtml = '';
    if (comment && comment.trim() !== '') {
        commentHtml = `<strong>Kommentar:</strong><br><em>${comment}</em><br>`;
    }

    let pictureHtml = '';
    if (pictureUrl && pictureUrl.trim() !== '') {
        pictureHtml = `<img src="${pictureUrl}" alt="Toilette" style="max-width: 200px; max-height: 150px; margin-top: 10px; border-radius: 5px; display: block;"><br>`;
    }

    const marker = L.marker([lat, lon]).addTo(window.map)
        .bindPopup(`${elementIdHtml}<strong>Type:</strong> ${type}<br>${commentHtml}${pictureHtml}${tagsHtml}`);

    window.markers.push(marker);
};

window.clearMarkers = function () {
    if (window.markers && window.markers.length > 0) {
        window.markers.forEach(marker => {
            window.map.removeLayer(marker);
        });
        window.markers = [];
        console.log("All markers cleared");
    }
};
*/
