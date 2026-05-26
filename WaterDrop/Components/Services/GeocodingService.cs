using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WaterDrop.Components.Services
{
	public class GeocodingService : IGeocodingService
	{
		private readonly IMemoryCache _cache;
		private readonly ILogger<GeocodingService> _logger;

		private static readonly Dictionary<string, BoundingBox> StaticBoundingBoxes = new(StringComparer.OrdinalIgnoreCase)
		{
			// === Großstädte (über 500.000 Einwohner) ===
			["Berlin"] = new BoundingBox { MinLat = 52.338, MaxLat = 52.676, MinLon = 13.088, MaxLon = 13.761, DisplayName = "Berlin, Deutschland" },
			["Hamburg"] = new BoundingBox { MinLat = 53.395, MaxLat = 53.745, MinLon = 9.731, MaxLon = 10.325, DisplayName = "Hamburg, Deutschland" },
			["München"] = new BoundingBox { MinLat = 48.061, MaxLat = 48.248, MinLon = 11.360, MaxLon = 11.723, DisplayName = "München, Bayern, Deutschland" },
			["Köln"] = new BoundingBox { MinLat = 50.837, MaxLat = 51.085, MinLon = 6.772, MaxLon = 7.162, DisplayName = "Köln, Nordrhein-Westfalen, Deutschland" },
			["Frankfurt"] = new BoundingBox { MinLat = 50.015, MaxLat = 50.227, MinLon = 8.473, MaxLon = 8.800, DisplayName = "Frankfurt am Main, Hessen, Deutschland" },
			["Stuttgart"] = new BoundingBox { MinLat = 48.692, MaxLat = 48.866, MinLon = 9.038, MaxLon = 9.315, DisplayName = "Stuttgart, Baden-Württemberg, Deutschland" },
			["Düsseldorf"] = new BoundingBox { MinLat = 51.113, MaxLat = 51.357, MinLon = 6.693, MaxLon = 6.938, DisplayName = "Düsseldorf, Nordrhein-Westfalen, Deutschland" },
			["Dortmund"] = new BoundingBox { MinLat = 51.417, MaxLat = 51.606, MinLon = 7.315, MaxLon = 7.632, DisplayName = "Dortmund, Nordrhein-Westfalen, Deutschland" },
			["Essen"] = new BoundingBox { MinLat = 51.367, MaxLat = 51.529, MinLon = 6.906, MaxLon = 7.146, DisplayName = "Essen, Nordrhein-Westfalen, Deutschland" },
			["Leipzig"] = new BoundingBox { MinLat = 51.236, MaxLat = 51.450, MinLon = 12.237, MaxLon = 12.546, DisplayName = "Leipzig, Sachsen, Deutschland" },
			["Bremen"] = new BoundingBox { MinLat = 53.009, MaxLat = 53.120, MinLon = 8.482, MaxLon = 8.989, DisplayName = "Bremen, Deutschland" },
			["Dresden"] = new BoundingBox { MinLat = 50.977, MaxLat = 51.173, MinLon = 13.588, MaxLon = 13.884, DisplayName = "Dresden, Sachsen, Deutschland" },
			["Hannover"] = new BoundingBox { MinLat = 52.302, MaxLat = 52.474, MinLon = 9.621, MaxLon = 9.899, DisplayName = "Hannover, Niedersachsen, Deutschland" },
			["Nürnberg"] = new BoundingBox { MinLat = 49.359, MaxLat = 49.501, MinLon = 10.998, MaxLon = 11.223, DisplayName = "Nürnberg, Bayern, Deutschland" },
			["Duisburg"] = new BoundingBox { MinLat = 51.363, MaxLat = 51.568, MinLon = 6.649, MaxLon = 6.841, DisplayName = "Duisburg, Nordrhein-Westfalen, Deutschland" },

			// === Großstädte (200.000 - 500.000 Einwohner) ===
			["Bochum"] = new BoundingBox { MinLat = 51.413, MaxLat = 51.543, MinLon = 7.114, MaxLon = 7.319, DisplayName = "Bochum, Nordrhein-Westfalen, Deutschland" },
			["Wuppertal"] = new BoundingBox { MinLat = 51.182, MaxLat = 51.317, MinLon = 7.003, MaxLon = 7.347, DisplayName = "Wuppertal, Nordrhein-Westfalen, Deutschland" },
			["Bielefeld"] = new BoundingBox { MinLat = 51.929, MaxLat = 52.092, MinLon = 8.442, MaxLon = 8.647, DisplayName = "Bielefeld, Nordrhein-Westfalen, Deutschland" },
			["Bonn"] = new BoundingBox { MinLat = 50.637, MaxLat = 50.774, MinLon = 7.014, MaxLon = 7.194, DisplayName = "Bonn, Nordrhein-Westfalen, Deutschland" },
			["Münster"] = new BoundingBox { MinLat = 51.840, MaxLat = 52.060, MinLon = 7.517, MaxLon = 7.774, DisplayName = "Münster, Nordrhein-Westfalen, Deutschland" },
			["Karlsruhe"] = new BoundingBox { MinLat = 48.939, MaxLat = 49.095, MinLon = 8.310, MaxLon = 8.504, DisplayName = "Karlsruhe, Baden-Württemberg, Deutschland" },
			["Mannheim"] = new BoundingBox { MinLat = 49.433, MaxLat = 49.547, MinLon = 8.431, MaxLon = 8.567, DisplayName = "Mannheim, Baden-Württemberg, Deutschland" },
			["Augsburg"] = new BoundingBox { MinLat = 48.297, MaxLat = 48.461, MinLon = 10.774, MaxLon = 11.002, DisplayName = "Augsburg, Bayern, Deutschland" },
			["Wiesbaden"] = new BoundingBox { MinLat = 50.011, MaxLat = 50.126, MinLon = 8.162, MaxLon = 8.389, DisplayName = "Wiesbaden, Hessen, Deutschland" },
			["Gelsenkirchen"] = new BoundingBox { MinLat = 51.456, MaxLat = 51.590, MinLon = 7.016, MaxLon = 7.142, DisplayName = "Gelsenkirchen, Nordrhein-Westfalen, Deutschland" },
			["Mönchengladbach"] = new BoundingBox { MinLat = 51.115, MaxLat = 51.268, MinLon = 6.357, MaxLon = 6.519, DisplayName = "Mönchengladbach, Nordrhein-Westfalen, Deutschland" },
			["Braunschweig"] = new BoundingBox { MinLat = 52.193, MaxLat = 52.330, MinLon = 10.444, MaxLon = 10.614, DisplayName = "Braunschweig, Niedersachsen, Deutschland" },
			["Chemnitz"] = new BoundingBox { MinLat = 50.767, MaxLat = 50.908, MinLon = 12.764, MaxLon = 13.002, DisplayName = "Chemnitz, Sachsen, Deutschland" },
			["Kiel"] = new BoundingBox { MinLat = 54.271, MaxLat = 54.410, MinLon = 10.040, MaxLon = 10.231, DisplayName = "Kiel, Schleswig-Holstein, Deutschland" },
			["Aachen"] = new BoundingBox { MinLat = 50.697, MaxLat = 50.828, MinLon = 6.007, MaxLon = 6.184, DisplayName = "Aachen, Nordrhein-Westfalen, Deutschland" },
			["Halle"] = new BoundingBox { MinLat = 51.418, MaxLat = 51.555, MinLon = 11.872, MaxLon = 12.039, DisplayName = "Halle (Saale), Sachsen-Anhalt, Deutschland" },
			["Magdeburg"] = new BoundingBox { MinLat = 52.062, MaxLat = 52.191, MinLon = 11.552, MaxLon = 11.717, DisplayName = "Magdeburg, Sachsen-Anhalt, Deutschland" },
			["Freiburg"] = new BoundingBox { MinLat = 47.930, MaxLat = 48.076, MinLon = 7.746, MaxLon = 7.918, DisplayName = "Freiburg im Breisgau, Baden-Württemberg, Deutschland" },
			["Krefeld"] = new BoundingBox { MinLat = 51.297, MaxLat = 51.398, MinLon = 6.501, MaxLon = 6.626, DisplayName = "Krefeld, Nordrhein-Westfalen, Deutschland" },
			["Lübeck"] = new BoundingBox { MinLat = 53.794, MaxLat = 53.945, MinLon = 10.619, MaxLon = 10.826, DisplayName = "Lübeck, Schleswig-Holstein, Deutschland" },
			["Oberhausen"] = new BoundingBox { MinLat = 51.460, MaxLat = 51.543, MinLon = 6.801, MaxLon = 6.916, DisplayName = "Oberhausen, Nordrhein-Westfalen, Deutschland" },
			["Erfurt"] = new BoundingBox { MinLat = 50.917, MaxLat = 51.027, MinLon = 10.968, MaxLon = 11.121, DisplayName = "Erfurt, Thüringen, Deutschland" },
			["Mainz"] = new BoundingBox { MinLat = 49.928, MaxLat = 50.034, MinLon = 8.185, MaxLon = 8.349, DisplayName = "Mainz, Rheinland-Pfalz, Deutschland" },
			["Rostock"] = new BoundingBox { MinLat = 54.031, MaxLat = 54.221, MinLon = 12.014, MaxLon = 12.238, DisplayName = "Rostock, Mecklenburg-Vorpommern, Deutschland" },

			// === Mittelstädte (100.000 - 200.000 Einwohner) ===
			["Kassel"] = new BoundingBox { MinLat = 51.270, MaxLat = 51.357, MinLon = 9.395, MaxLon = 9.561, DisplayName = "Kassel, Hessen, Deutschland" },
			["Hagen"] = new BoundingBox { MinLat = 51.311, MaxLat = 51.432, MinLon = 7.396, MaxLon = 7.560, DisplayName = "Hagen, Nordrhein-Westfalen, Deutschland" },
			["Hamm"] = new BoundingBox { MinLat = 51.624, MaxLat = 51.717, MinLon = 7.747, MaxLon = 7.935, DisplayName = "Hamm, Nordrhein-Westfalen, Deutschland" },
			["Saarbrücken"] = new BoundingBox { MinLat = 49.195, MaxLat = 49.273, MinLon = 6.930, MaxLon = 7.063, DisplayName = "Saarbrücken, Saarland, Deutschland" },
			["Mülheim"] = new BoundingBox { MinLat = 51.407, MaxLat = 51.471, MinLon = 6.829, MaxLon = 6.923, DisplayName = "Mülheim an der Ruhr, Nordrhein-Westfalen, Deutschland" },
			["Potsdam"] = new BoundingBox { MinLat = 52.340, MaxLat = 52.457, MinLon = 12.972, MaxLon = 13.160, DisplayName = "Potsdam, Brandenburg, Deutschland" },
			["Ludwigshafen"] = new BoundingBox { MinLat = 49.457, MaxLat = 49.530, MinLon = 8.382, MaxLon = 8.478, DisplayName = "Ludwigshafen am Rhein, Rheinland-Pfalz, Deutschland" },
			["Oldenburg"] = new BoundingBox { MinLat = 53.089, MaxLat = 53.194, MinLon = 8.149, MaxLon = 8.266, DisplayName = "Oldenburg, Niedersachsen, Deutschland" },
			["Leverkusen"] = new BoundingBox { MinLat = 51.007, MaxLat = 51.094, MinLon = 6.936, MaxLon = 7.062, DisplayName = "Leverkusen, Nordrhein-Westfalen, Deutschland" },
			["Osnabrück"] = new BoundingBox { MinLat = 52.228, MaxLat = 52.321, MinLon = 7.932, MaxLon = 8.110, DisplayName = "Osnabrück, Niedersachsen, Deutschland" },
			["Solingen"] = new BoundingBox { MinLat = 51.133, MaxLat = 51.211, MinLon = 7.034, MaxLon = 7.141, DisplayName = "Solingen, Nordrhein-Westfalen, Deutschland" },
			["Heidelberg"] = new BoundingBox { MinLat = 49.374, MaxLat = 49.453, MinLon = 8.629, MaxLon = 8.744, DisplayName = "Heidelberg, Baden-Württemberg, Deutschland" },
			["Herne"] = new BoundingBox { MinLat = 51.520, MaxLat = 51.575, MinLon = 7.174, MaxLon = 7.269, DisplayName = "Herne, Nordrhein-Westfalen, Deutschland" },
			["Neuss"] = new BoundingBox { MinLat = 51.167, MaxLat = 51.236, MinLon = 6.656, MaxLon = 6.761, DisplayName = "Neuss, Nordrhein-Westfalen, Deutschland" },
			["Darmstadt"] = new BoundingBox { MinLat = 49.826, MaxLat = 49.913, MinLon = 8.599, MaxLon = 8.701, DisplayName = "Darmstadt, Hessen, Deutschland" },
			["Paderborn"] = new BoundingBox { MinLat = 51.675, MaxLat = 51.757, MinLon = 8.698, MaxLon = 8.796, DisplayName = "Paderborn, Nordrhein-Westfalen, Deutschland" },
			["Regensburg"] = new BoundingBox { MinLat = 48.983, MaxLat = 49.083, MinLon = 12.033, MaxLon = 12.161, DisplayName = "Regensburg, Bayern, Deutschland" },
			["Ingolstadt"] = new BoundingBox { MinLat = 48.715, MaxLat = 48.800, MinLon = 11.377, MaxLon = 11.490, DisplayName = "Ingolstadt, Bayern, Deutschland" },
			["Würzburg"] = new BoundingBox { MinLat = 49.751, MaxLat = 49.837, MinLon = 9.869, MaxLon = 10.002, DisplayName = "Würzburg, Bayern, Deutschland" },
			["Fürth"] = new BoundingBox { MinLat = 49.438, MaxLat = 49.513, MinLon = 10.957, MaxLon = 11.053, DisplayName = "Fürth, Bayern, Deutschland" },
			["Wolfsburg"] = new BoundingBox { MinLat = 52.383, MaxLat = 52.476, MinLon = 10.731, MaxLon = 10.852, DisplayName = "Wolfsburg, Niedersachsen, Deutschland" },
			["Offenbach"] = new BoundingBox { MinLat = 50.077, MaxLat = 50.128, MinLon = 8.725, MaxLon = 8.812, DisplayName = "Offenbach am Main, Hessen, Deutschland" },
			["Ulm"] = new BoundingBox { MinLat = 48.364, MaxLat = 48.437, MinLon = 9.935, MaxLon = 10.044, DisplayName = "Ulm, Baden-Württemberg, Deutschland" },
			["Heilbronn"] = new BoundingBox { MinLat = 49.114, MaxLat = 49.180, MinLon = 9.174, MaxLon = 9.264, DisplayName = "Heilbronn, Baden-Württemberg, Deutschland" },
			["Pforzheim"] = new BoundingBox { MinLat = 48.867, MaxLat = 48.920, MinLon = 8.659, MaxLon = 8.757, DisplayName = "Pforzheim, Baden-Württemberg, Deutschland" },
			["Göttingen"] = new BoundingBox { MinLat = 51.495, MaxLat = 51.577, MinLon = 9.866, MaxLon = 9.987, DisplayName = "Göttingen, Niedersachsen, Deutschland" },
			["Bottrop"] = new BoundingBox { MinLat = 51.501, MaxLat = 51.574, MinLon = 6.881, MaxLon = 6.996, DisplayName = "Bottrop, Nordrhein-Westfalen, Deutschland" },
			["Trier"] = new BoundingBox { MinLat = 49.712, MaxLat = 49.781, MinLon = 6.585, MaxLon = 6.705, DisplayName = "Trier, Rheinland-Pfalz, Deutschland" },
			["Recklinghausen"] = new BoundingBox { MinLat = 51.567, MaxLat = 51.632, MinLon = 7.149, MaxLon = 7.247, DisplayName = "Recklinghausen, Nordrhein-Westfalen, Deutschland" },
			["Reutlingen"] = new BoundingBox { MinLat = 48.463, MaxLat = 48.528, MinLon = 9.177, MaxLon = 9.261, DisplayName = "Reutlingen, Baden-Württemberg, Deutschland" },
			["Bremerhaven"] = new BoundingBox { MinLat = 53.490, MaxLat = 53.587, MinLon = 8.541, MaxLon = 8.632, DisplayName = "Bremerhaven, Deutschland" },
			["Koblenz"] = new BoundingBox { MinLat = 50.319, MaxLat = 50.396, MinLon = 7.539, MaxLon = 7.651, DisplayName = "Koblenz, Rheinland-Pfalz, Deutschland" },
			["Bergisch Gladbach"] = new BoundingBox { MinLat = 50.958, MaxLat = 51.028, MinLon = 7.089, MaxLon = 7.179, DisplayName = "Bergisch Gladbach, Nordrhein-Westfalen, Deutschland" },
			["Jena"] = new BoundingBox { MinLat = 50.870, MaxLat = 50.959, MinLon = 11.551, MaxLon = 11.660, DisplayName = "Jena, Thüringen, Deutschland" },
			["Remscheid"] = new BoundingBox { MinLat = 51.140, MaxLat = 51.224, MinLon = 7.155, MaxLon = 7.263, DisplayName = "Remscheid, Nordrhein-Westfalen, Deutschland" },
			["Erlangen"] = new BoundingBox { MinLat = 49.558, MaxLat = 49.624, MinLon = 10.968, MaxLon = 11.054, DisplayName = "Erlangen, Bayern, Deutschland" },
			["Moers"] = new BoundingBox { MinLat = 51.426, MaxLat = 51.487, MinLon = 6.596, MaxLon = 6.682, DisplayName = "Moers, Nordrhein-Westfalen, Deutschland" },
			["Siegen"] = new BoundingBox { MinLat = 50.833, MaxLat = 50.905, MinLon = 7.972, MaxLon = 8.079, DisplayName = "Siegen, Nordrhein-Westfalen, Deutschland" },
			["Hildesheim"] = new BoundingBox { MinLat = 52.123, MaxLat = 52.189, MinLon = 9.911, MaxLon = 10.016, DisplayName = "Hildesheim, Niedersachsen, Deutschland" },
			["Salzgitter"] = new BoundingBox { MinLat = 52.054, MaxLat = 52.194, MinLon = 10.274, MaxLon = 10.474, DisplayName = "Salzgitter, Niedersachsen, Deutschland" },

			// === Weitere wichtige Städte ===
			["Cottbus"] = new BoundingBox { MinLat = 51.721, MaxLat = 51.801, MinLon = 14.282, MaxLon = 14.394, DisplayName = "Cottbus, Brandenburg, Deutschland" },
			["Gütersloh"] = new BoundingBox { MinLat = 51.881, MaxLat = 51.927, MinLon = 8.349, MaxLon = 8.428, DisplayName = "Gütersloh, Nordrhein-Westfalen, Deutschland" },
			["Witten"] = new BoundingBox { MinLat = 51.415, MaxLat = 51.473, MinLon = 7.305, MaxLon = 7.395, DisplayName = "Witten, Nordrhein-Westfalen, Deutschland" },
			["Schwerin"] = new BoundingBox { MinLat = 53.584, MaxLat = 53.662, MinLon = 11.351, MaxLon = 11.463, DisplayName = "Schwerin, Mecklenburg-Vorpommern, Deutschland" },
			["Gera"] = new BoundingBox { MinLat = 50.846, MaxLat = 50.912, MinLon = 12.043, MaxLon = 12.140, DisplayName = "Gera, Thüringen, Deutschland" },
			["Iserlohn"] = new BoundingBox { MinLat = 51.351, MaxLat = 51.409, MinLon = 7.634, MaxLon = 7.731, DisplayName = "Iserlohn, Nordrhein-Westfalen, Deutschland" },
			["Zwickau"] = new BoundingBox { MinLat = 50.685, MaxLat = 50.752, MinLon = 12.449, MaxLon = 12.547, DisplayName = "Zwickau, Sachsen, Deutschland" },
			["Düren"] = new BoundingBox { MinLat = 50.781, MaxLat = 50.828, MinLon = 6.454, MaxLon = 6.522, DisplayName = "Düren, Nordrhein-Westfalen, Deutschland" },
			["Esslingen"] = new BoundingBox { MinLat = 48.726, MaxLat = 48.770, MinLon = 9.286, MaxLon = 9.348, DisplayName = "Esslingen am Neckar, Baden-Württemberg, Deutschland" },
			["Ratingen"] = new BoundingBox { MinLat = 51.279, MaxLat = 51.333, MinLon = 6.823, MaxLon = 6.895, DisplayName = "Ratingen, Nordrhein-Westfalen, Deutschland" },
			["Lünen"] = new BoundingBox { MinLat = 51.586, MaxLat = 51.645, MinLon = 7.479, MaxLon = 7.558, DisplayName = "Lünen, Nordrhein-Westfalen, Deutschland" },
			["Hanau"] = new BoundingBox { MinLat = 50.105, MaxLat = 50.157, MinLon = 8.880, MaxLon = 8.964, DisplayName = "Hanau, Hessen, Deutschland" },
			["Ludwigsburg"] = new BoundingBox { MinLat = 48.879, MaxLat = 48.915, MinLon = 9.168, MaxLon = 9.227, DisplayName = "Ludwigsburg, Baden-Württemberg, Deutschland" },
			["Velbert"] = new BoundingBox { MinLat = 51.316, MaxLat = 51.368, MinLon = 7.024, MaxLon = 7.099, DisplayName = "Velbert, Nordrhein-Westfalen, Deutschland" },
			["Flensburg"] = new BoundingBox { MinLat = 54.770, MaxLat = 54.830, MinLon = 9.396, MaxLon = 9.483, DisplayName = "Flensburg, Schleswig-Holstein, Deutschland" },
			["Wilhelmshaven"] = new BoundingBox { MinLat = 53.494, MaxLat = 53.564, MinLon = 8.030, MaxLon = 8.152, DisplayName = "Wilhelmshaven, Niedersachsen, Deutschland" },
			["Konstanz"] = new BoundingBox { MinLat = 47.645, MaxLat = 47.703, MinLon = 9.133, MaxLon = 9.224, DisplayName = "Konstanz, Baden-Württemberg, Deutschland" },
			["Worms"] = new BoundingBox { MinLat = 49.609, MaxLat = 49.667, MinLon = 8.325, MaxLon = 8.409, DisplayName = "Worms, Rheinland-Pfalz, Deutschland" },
			["Dorsten"] = new BoundingBox { MinLat = 51.636, MaxLat = 51.711, MinLon = 6.939, MaxLon = 7.036, DisplayName = "Dorsten, Nordrhein-Westfalen, Deutschland" },
			["Norderstedt"] = new BoundingBox { MinLat = 53.666, MaxLat = 53.723, MinLon = 9.976, MaxLon = 10.040, DisplayName = "Norderstedt, Schleswig-Holstein, Deutschland" },
			["Marburg"] = new BoundingBox { MinLat = 50.786, MaxLat = 50.839, MinLon = 8.739, MaxLon = 8.810, DisplayName = "Marburg, Hessen, Deutschland" },
			["Dessau"] = new BoundingBox { MinLat = 51.809, MaxLat = 51.873, MinLon = 12.199, MaxLon = 12.283, DisplayName = "Dessau-Roßlau, Sachsen-Anhalt, Deutschland" },
			["Weimar"] = new BoundingBox { MinLat = 50.958, MaxLat = 51.006, MinLon = 11.300, MaxLon = 11.368, DisplayName = "Weimar, Thüringen, Deutschland" },
			["Bamberg"] = new BoundingBox { MinLat = 49.869, MaxLat = 49.916, MinLon = 10.860, MaxLon = 10.929, DisplayName = "Bamberg, Bayern, Deutschland" },
			["Aschaffenburg"] = new BoundingBox { MinLat = 49.944, MaxLat = 50.003, MinLon = 9.119, MaxLon = 9.188, DisplayName = "Aschaffenburg, Bayern, Deutschland" },
			["Lüneburg"] = new BoundingBox { MinLat = 53.226, MaxLat = 53.277, MinLon = 10.377, MaxLon = 10.453, DisplayName = "Lüneburg, Niedersachsen, Deutschland" },
			["Landshut"] = new BoundingBox { MinLat = 48.519, MaxLat = 48.567, MinLon = 12.133, MaxLon = 12.201, DisplayName = "Landshut, Bayern, Deutschland" },
			["Fulda"] = new BoundingBox { MinLat = 50.528, MaxLat = 50.579, MinLon = 9.647, MaxLon = 9.714, DisplayName = "Fulda, Hessen, Deutschland" },
		};

		public GeocodingService(IMemoryCache cache, ILogger<GeocodingService> logger)
		{
			_cache = cache;
			_logger = logger;
		}

		public Task<BoundingBox?> GetCityBoundingBoxAsync(string city)
		{
			_logger.LogInformation("GetCityBoundingBoxAsync aufgerufen mit: '{City}'", city);
			
			if (StaticBoundingBoxes.TryGetValue(city, out var staticBBox))
			{
				_logger.LogInformation("Statische Bounding Box für {City} gefunden: {BBox}", city, staticBBox);
				return Task.FromResult<BoundingBox?>(staticBBox);
			}

			_logger.LogWarning("Stadt '{City}' nicht in statischer Liste gefunden", city);
			return Task.FromResult<BoundingBox?>(null);
		}

		public IReadOnlyDictionary<string, BoundingBox> GetAllCities() => StaticBoundingBoxes;
	}

	public class BoundingBox
	{
		public double MinLat { get; set; }
		public double MaxLat { get; set; }
		public double MinLon { get; set; }
		public double MaxLon { get; set; }
		public string DisplayName { get; set; }

		public override string ToString()
		{
			return $"[{MinLat:F4},{MinLon:F4}] → [{MaxLat:F4},{MaxLon:F4}]";
		}
	}
}