﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMBC.Suppliers.API.ConfigurationModule.Models.Dynamics;
using EMBC.Suppliers.API.ConfigurationModule.ViewModels;

namespace EMBC.Suppliers.API.ConfigurationModule.Models
{
    public class ListsProvider :
        ICountriesListProvider,
        IStateProvincesListProvider,
        IJurisdictionsListProvider,
        ISupportsListProvider
    {
        private readonly ICachedListsProvider cache;

        public ListsProvider(ICachedListsProvider cache)
        {
            this.cache = cache;
        }

        public async Task<IEnumerable<Country>> GetCountriesAsync()
        {
            var countries = await cache.GetCountriesAsync();
            return countries.Select(c => new Country { Code = c.era_countrycode, Name = c.era_name });
        }

        public async Task<IEnumerable<Jurisdiction>> GetJurisdictionsAsync(string[] types, string stateProvinceCode, string countryCode)
        {
            var country = (await cache.GetCountriesAsync()).SingleOrDefault(c => c.era_countrycode == countryCode);
            if (country == null) return Array.Empty<Jurisdiction>();
            var stateProvince = (await cache.GetStateProvincesAsync()).SingleOrDefault(sp => sp._era_relatedcountry_value == country.era_countryid && sp.era_code == stateProvinceCode);
            if (stateProvince == null) return Array.Empty<Jurisdiction>();

            return (await cache.GetJurisdictionsAsync())
                .Where(j => j._era_relatedprovincestate_value == stateProvince.era_provinceterritoriesid &&
                    (!types.Any() || types.Any(t => t.Equals(j.era_type))))
                .Select(j => new Jurisdiction
                {
                    Code = j.era_jurisdictionid,
                    Name = j.era_jurisdictionname,
                    Type = j.era_type,
                    StateProvinceCode = stateProvinceCode,
                    CountryCode = countryCode
                });
        }

        public async Task<IEnumerable<StateProvince>> GetStateProvincesAsync(string countryCode)
        {
            var country = (await cache.GetCountriesAsync()).SingleOrDefault(c => c.era_countrycode == countryCode);
            if (country == null) return Array.Empty<StateProvince>();
            return (await cache.GetStateProvincesAsync())
                .Where(sp => sp._era_relatedcountry_value == country.era_countryid)
                .Select(sp => new StateProvince { Code = sp.era_code, Name = sp.era_name, CountryCode = countryCode });
        }

        public async Task<IEnumerable<Support>> GetSupportsAsync()
        {
            var supports = await cache.GetSupportsAsync();
            return supports.Select(c => new Support { Code = c.era_name, Name = c.era_name });
        }
    }
}
