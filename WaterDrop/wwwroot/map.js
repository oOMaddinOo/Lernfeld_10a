window.map = null;
window.markers = [];

window.initMap = function (lat, lon) {

    window.map = L.map('map').setView([lat, lon], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(window.map);

    /* L.marker([lat, lon]).addTo(window.map);*/
};

window.addMarker = function (lat, lon, type, tags) {

    if (!window.map) {
        console.error("Map not initialized!");
        return;
    }

    // Dictionary/Objekt zu HTML-Liste formatieren
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

    const marker = L.marker([lat, lon]).addTo(window.map)
        .bindPopup(`<strong>Type:</strong> ${type}<br>${tagsHtml}`);

    window.markers.push(marker);
};

// Zentriert die Karte auf einen Punkt
window.centerMap = function (lat, lon, zoom = 13) {
    if (window.map) {
        window.map.setView([lat, lon], zoom);
    } else {
        console.error("Map not initialized!");
    }
};

// Entfernt alle Marker von der Karte
window.clearMarkers = function () {
    if (window.markers && window.markers.length > 0) {
        window.markers.forEach(marker => {
            window.map.removeLayer(marker);
        });
        window.markers = [];
        console.log("All markers cleared");
    }
};