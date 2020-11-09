using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WorkerService.converters;

namespace WorkerService
{
    class CountryStats
    {
        [JsonProperty("last_update")]
        public string LastUpdated { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("country_abbreviation")]
        public string CountryAbbreviation { get; set; }

        [JsonProperty("total_cases")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int TotalCases { get; set; }

        [JsonProperty("new_cases")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int NewCases { get; set; }

        [JsonProperty("total_deaths")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int TotalDeaths { get; set; }

        [JsonProperty("new_deaths")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int NewDeaths { get; set; }

        [JsonProperty("total_recovered")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int TotalRecovered { get; set; }

        [JsonProperty("active_cases")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int ActiveCases { get; set; }

        [JsonProperty("serious_critical")]
        [JsonConverter(typeof(CountryStatsIntConverter))]
        public int SeriousCritical { get; set; }

        [JsonProperty("cases_per_mill_pop")]
        public double CasesPerMillPop { get; set; }

        [JsonProperty("flag")]
        public string FlagUrl { get; set; }

    }
}
