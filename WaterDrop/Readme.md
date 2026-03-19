# 🚰 WaterDrop

**WaterDrop** ist eine interaktive Webanwendung, die Benutzern hilft, öffentliche Toiletten, Trinkwasserbrunnen und barrierefreie Einrichtungen in ihrer Umgebung zu finden. Die App nutzt OpenStreetMap-Daten über die Overpass API und zeigt die Ergebnisse auf einer interaktiven Leaflet-Karte an.

## 🎯 Features

- 🗺️ **Interaktive Karte**: Visualisierung von Einrichtungen auf einer Leaflet-Karte
- 🔍 **Stadtsuche**: Suche nach Toiletten und Einrichtungen in einer bestimmten Stadt
- 🎨 **Kategoriefilter**:
  - Toiletten (Restrooms)
  - Trinkwasser (Drinking Water)
  - Barrierefreie Einrichtungen (Accessible)
  - Wickeltische (Changing Table)
- 📱 **Responsive Design**: Desktop- und Mobile-Ansicht mit Bottom-Sheet-Navigation
- 📍 **Geolokalisierung**: Automatische Standortbestimmung
- ⭐ **Bewertungssystem**: Nutzer können Kommentare zu Einrichtungen hinterlassen
- 💾 **Persistente Daten**: Bewertungen werden in SQL Server gespeichert

## 🛠️ Technologie-Stack

- **Framework**: Blazor Web App (.NET 10)
- **Render Mode**: Interactive Server
- **Datenbank**: SQL Server mit Entity Framework Core
- **API**: Overpass API (OpenStreetMap)
- **Kartenbibliothek**: Leaflet.js
- **UI**: Razor Components mit Custom CSS

## 📋 Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB oder vollständige Installation)
- Moderner Webbrowser mit JavaScript-Unterstützung

## 🔗 Links

- [Repository](https://github.com/oOMaddinOo/Lernfeld_10a)
- [Overpass API](https://overpass-api.de)
- [OpenStreetMap](https://www.openstreetmap.org)
- [Leaflet.js](https://leafletjs.com)
