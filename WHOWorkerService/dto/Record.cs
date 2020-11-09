using System;
using System.Collections.Generic;
using System.Text;

namespace WHOWorkerService.dto
{
    class Record
    {
        public DateTime DateReported { get; set; }
        public string CountryCode { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public int NewCases { get; set; }
        public int CumulativeCases { get; set; }
        public int NewDeaths { get; set; }
        public int CumulativeDeaths { get; set; }
    }
}
