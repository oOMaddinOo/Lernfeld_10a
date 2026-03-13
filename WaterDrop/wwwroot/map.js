window.map = null;
window.markers = [];

window.initMap = function (lat, lon) {

    window.map = L.map('map').setView([lat, lon], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(window.map);

    /* L.marker([lat, lon]).addTo(window.map);*/
};

window.addMarker = function (elementId, lat, lon, type, tags, comment, pictureUrl) {

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

    // ElementId formatieren, falls vorhanden
    let elementIdHtml = '';
    if (elementId) {
        elementIdHtml = `<strong>Id:</strong> ${elementId}<br>`;
    }

    // Kommentar formatieren, falls vorhanden
    let commentHtml = '';
    if (comment && comment.trim() !== '') {
        commentHtml = `<strong>Kommentar:</strong><br><em>${comment}</em><br>`;
    }

    // Bild formatieren, falls vorhanden
    let pictureHtml = '';
    if (pictureUrl && pictureUrl.trim() !== '') {
        pictureHtml = `<img src="${pictureUrl}" alt="Toilette" style="max-width: 200px; max-height: 150px; margin-top: 10px; border-radius: 5px; display: block;"><br>`;
    }

    const marker = L.marker([lat, lon]).addTo(window.map)
        .bindPopup(`${elementIdHtml}<strong>Type:</strong> ${type}<br>${commentHtml}${pictureHtml}${tagsHtml}`);

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