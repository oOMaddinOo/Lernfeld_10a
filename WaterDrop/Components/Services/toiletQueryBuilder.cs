namespace WaterDrop.Components.Services
{
	public class ToiletQueryBuilder
	{
		private string _city = "Hamburg";
		private int _timeout = 30;
		private string _amenityType = "toilets";
		private bool _includeFee = false;
		private string _accessType = null;

		public ToiletQueryBuilder SetCity(string city)
		{
			_city = city;
			return this;
		}

		public ToiletQueryBuilder SetTimeout(int timeout)
		{
			_timeout = timeout;
			return this;
		}

		public ToiletQueryBuilder SetAmenityType(string amenityType)
		{
			_amenityType = amenityType;
			return this;
		}

		public ToiletQueryBuilder IncludeFeeInformation(bool include)
		{
			_includeFee = include;
			return this;
		}

		public ToiletQueryBuilder SetAccessType(string accessType)
		{
			_accessType = accessType;
			return this;
		}

		public string Build()
		{
			var query = $@"[out:json][timeout:{_timeout}];
                area[""name""=""{_city}""][""boundary""=""administrative""]->.searchArea;
                (
                  node[""amenity""=""{_amenityType}""]";

			if (_accessType != null)
			{
				query += $@"[""access""=""{_accessType}""]";
			}

			if (_includeFee)
			{
				query += @"[""fee""]";
			}

			query += @"(area.searchArea);
                  way[""amenity""=""toilets""](area.searchArea);
                  relation[""amenity""=""toilets""](area.searchArea);
                );
                out center;";

			return query;
		}
	}
}