using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WHOWorkerService.dto;

namespace WHOWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("WHO Worker running at: {time}", DateTimeOffset.Now);

                    _logger.LogInformation("Fetching WHO-COVID-19-global-data.csv");

                    using (WebClient client = new WebClient())
                    using (var reader = new StreamReader(client.OpenRead("https://covid19.who.int/WHO-COVID-19-global-data.csv")))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = new List<Record>();

                        csv.Read();
                        csv.ReadHeader();
                        while (csv.Read())
                        {
                            var record = new Record
                            {
                                DateReported = csv.GetField<DateTime>("Date_reported"),
                                CountryCode = csv.GetField(" Country_code"),
                                Country = csv.GetField(" Country"),
                                Region = csv.GetField(" WHO_region"),
                                NewCases = csv.GetField<int>(" New_cases"),
                                CumulativeCases = csv.GetField<int>(" Cumulative_cases"),
                                NewDeaths = csv.GetField<int>(" New_deaths"),
                                CumulativeDeaths = csv.GetField<int>(" Cumulative_deaths")
                            };

                            records.Add(record);
                        }

                        _logger.LogInformation("Found {count} Records, saving to DB", records.Count);

                        _savetoDb2(records);

                        _logger.LogInformation("Save to DB finished");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Error thrown : {err}",e.Message);
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }


        private void _savetoDb(List<Record> records)
        {
            string sqlServer = Environment.GetEnvironmentVariable("MS_SQL_SERVER");
            DataTable dt = new DataTable();

            foreach (Record record in records)
            {
                using (SqlConnection sqlConn = new SqlConnection($"Server={sqlServer};Database=CovidTracker;User Id=sa;Password=Passw@rd!;"))
                {
                    string sql = "insert_who_record";

                    using (SqlCommand sqlCmd = new SqlCommand(sql, sqlConn))
                    {
                        sqlCmd.CommandType = CommandType.StoredProcedure;

                        sqlCmd.Parameters.AddWithValue("@date", record.DateReported);
                        sqlCmd.Parameters.AddWithValue("@country", record.Country);
                        sqlCmd.Parameters.AddWithValue("@country_code", record.CountryCode);
                        sqlCmd.Parameters.AddWithValue("@region", record.Region);
                        sqlCmd.Parameters.AddWithValue("@new_cases", record.NewCases);
                        sqlCmd.Parameters.AddWithValue("@cumulative_cases", record.CumulativeCases);
                        sqlCmd.Parameters.AddWithValue("@new_deaths", record.NewDeaths);
                        sqlCmd.Parameters.AddWithValue("@cumulative_deaths", record.CumulativeDeaths);


                        sqlConn.Open();
                        using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
                        {
                            sqlAdapter.Fill(dt);
                        }
                    }
                }
            }
        }

        private void _savetoDb2(List<Record> records)
        {
            string sqlServer = Environment.GetEnvironmentVariable("MS_SQL_SERVER");

            int rowCnt = 0;
            int batchSize = 10000;

            using (SqlConnection sqlConn = new SqlConnection($"Server={sqlServer};Database=CovidTracker;User Id=sa;Password=Passw@rd!;"))
            {
                DataTable dt = new DataTable();

                dt.Columns.Add("date", typeof(DateTime));
                dt.Columns.Add("country", typeof(string));
                dt.Columns.Add("country_code", typeof(string));
                dt.Columns.Add("region", typeof(string));
                dt.Columns.Add("new_cases", typeof(int));
                dt.Columns.Add("cumulative_cases", typeof(int));
                dt.Columns.Add("new_deaths", typeof(int));
                dt.Columns.Add("cumulative_deaths", typeof(int));

                sqlConn.Open();

                foreach (var record in records)
                {
                    dt.Rows.Add(new object[]
                    {
                       record.DateReported,
                       record.Country,
                       record.CountryCode,
                       record.Region,
                       record.NewCases,
                       record.CumulativeCases,
                       record.NewDeaths,
                       record.CumulativeDeaths,
                    });

                    if (++rowCnt % batchSize == 0)
                    {
                        _executeSP(dt, sqlConn);
                        dt.Clear();
                    }
                }

                _executeSP(dt, sqlConn);
                sqlConn.Close();
            }
        }

        private void _executeSP(DataTable dt, SqlConnection connection)
        {
            using (SqlCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = "insert_who_recors";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 300;
                cmd.Parameters.AddWithValue("@table", dt);

                cmd.ExecuteScalar();
            }

        }

    }
}
