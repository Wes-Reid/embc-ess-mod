﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EMBC.Suppliers.API.ConfigurationModule.Models.Dynamics;
using EMBC.Suppliers.API.SubmissionModule.ViewModels;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xrm.Tools.WebAPI;

namespace EMBC.Suppliers.API.SubmissionModule.Models.Dynamics
{
    public class SubmissionDynamicsCustomActionHandler : ISubmissionDynamicsCustomActionHandler
    {
        private readonly CRMWebAPI api;
        private readonly ILogger<SubmissionDynamicsCustomActionHandler> logger;
        private readonly ICachedListsProvider cachedListsProvider;

        public SubmissionDynamicsCustomActionHandler(CRMWebAPI api, ILogger<SubmissionDynamicsCustomActionHandler> logger, ICachedListsProvider cachedListsProvider)
        {
            this.api = api;
            this.logger = logger;
            this.cachedListsProvider = cachedListsProvider;
        }

        public async Task Handle(SubmissionSavedEvent evt)
        {
            var submissions = new[] { evt.Submission }
                .Select(async s => await ResolveEntitiesReferences(s))
                .Select(t => t.Result)
                .MapSubmissions(evt.ReferenceNumber);

            try
            {
                foreach (var submission in submissions)
                {
                    logger.LogDebug(JsonConvert.SerializeObject(submission));

                    dynamic result = await api.ExecuteAction("era_SubmitUnauthInvoices", submission);

                    if (!result.submissionFlag)
                    {
                        throw new Exception($"era_SubmitUnauthInvoices call failed: {result.message}");
                    }
                }
            }
            catch (ValidationException e)
            {
                throw new Exception($"Submission '{evt.ReferenceNumber}' validation error: {e.Message}", e);
            }
            catch (AggregateException e)
            {
                throw new Exception($"Submission '{evt.ReferenceNumber}' validation error: {e.Message}", e);
            }
        }

        private async Task<Submission> ResolveEntitiesReferences(Submission submission)
        {
            var countries = (await cachedListsProvider.GetCountriesAsync()).ToArray();
            var stateProvinces = (await cachedListsProvider.GetStateProvincesAsync()).ToArray();
            var jurisdictions = (await cachedListsProvider.GetJurisdictionsAsync()).ToArray();
            var supports = (await cachedListsProvider.GetSupportsAsync()).ToArray();

            foreach (var supplierInformation in submission.Suppliers)
            {
                var cityCode = supplierInformation?.Address?.CityCode;
                if (cityCode != null)
                {
                    var jurisdiction = jurisdictions.SingleOrDefault(j => j.era_jurisdictionid.Equals(cityCode, StringComparison.OrdinalIgnoreCase));
                    if (jurisdiction == null) throw new ValidationException($"City code '{cityCode}' doesn't exists in Dynamics");
                    supplierInformation.Address.CityCode = jurisdiction.era_jurisdictionid;
                }

                var stateProvinceCode = supplierInformation?.Address?.StateProvinceCode;
                if (stateProvinceCode != null)
                {
                    var stateProvince = stateProvinces.SingleOrDefault(sp => sp.era_code.Equals(stateProvinceCode, StringComparison.OrdinalIgnoreCase));
                    if (stateProvince == null) throw new ValidationException($"StateProvinceCode '{stateProvinceCode}' doesn't exists in Dynamics");
                    supplierInformation.Address.StateProvinceCode = stateProvince.era_provinceterritoriesid;
                }

                var countryCode = supplierInformation?.Address?.Country;
                if (countryCode != null)
                {
                    var country = countries.SingleOrDefault(c => c.era_countrycode.Equals(supplierInformation.Address?.CountryCode, StringComparison.OrdinalIgnoreCase));
                    if (country == null) throw new ValidationException($"CountryCode '{supplierInformation?.Address?.CountryCode}' doesn't exists in Dynamics");
                    supplierInformation.Address.CountryCode = country.era_countryid;
                }
            }

            foreach (var lineItem in submission.LineItems)
            {
                var support = supports.SingleOrDefault(s => s.era_name.Equals(lineItem?.SupportProvided, StringComparison.OrdinalIgnoreCase));
                if (support == null) throw new ValidationException($"in line item referral {lineItem?.ReferralNumber}, SupportProvided '{lineItem?.SupportProvided}' is null or doesn't exists in Dynamics");
                lineItem.SupportProvided = support.era_supportid;
            }

            return submission;
        }
    }
}
