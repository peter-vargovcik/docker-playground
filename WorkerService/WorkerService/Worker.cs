using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WorkerService
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
            string rabbitMQHost = Environment.GetEnvironmentVariable("RABBIT_MQ_HOST");
            _logger.LogInformation($"RABBIT_MQ_HOST is set to {rabbitMQHost}");
            //for RabbitMQ messaging queue

            var factory = new ConnectionFactory() { HostName = rabbitMQHost };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "covid-19-stats",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation(message);

                    CountryStats countryStats = JsonConvert.DeserializeObject<CountryStats>(message);
                    _processCoutryStats(countryStats);

                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: "covid-19-stats",
                                     autoAck: false,
                                     consumer: consumer);
                _logger.LogInformation("Queue subscribed");

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker Hearthbeat at: {time}", DateTimeOffset.Now);
                    await Task.Delay(1000 * 60, stoppingToken);
                }
            }           
        }

        private void _processCoutryStats(CountryStats countryStats)
        {

            _savetoDb(countryStats);
        }

        private void _savetoDb(CountryStats countryStats)
        {
            string sqlServer = Environment.GetEnvironmentVariable("MS_SQL_SERVER");
            DataTable dt = new DataTable();

            using (SqlConnection sqlConn = new SqlConnection($"Server={sqlServer};Database=CovidTracker;User Id=sa;Password=Passw@rd!;"))
            {
                string sql = "insert_country";
                using (SqlCommand sqlCmd = new SqlCommand(sql, sqlConn))
                {
                    sqlCmd.CommandType = CommandType.StoredProcedure;

                    sqlCmd.Parameters.AddWithValue("@last_update", countryStats.LastUpdated);
                    sqlCmd.Parameters.AddWithValue("@country", countryStats.Country);
                    sqlCmd.Parameters.AddWithValue("@country_abbreviation", countryStats.CountryAbbreviation);
                    sqlCmd.Parameters.AddWithValue("@total_cases", countryStats.TotalCases);
                    sqlCmd.Parameters.AddWithValue("@new_cases", countryStats.NewCases);
                    sqlCmd.Parameters.AddWithValue("@total_deaths", countryStats.TotalDeaths);
                    sqlCmd.Parameters.AddWithValue("@new_deaths", countryStats.NewDeaths);
                    sqlCmd.Parameters.AddWithValue("@total_recovered", countryStats.TotalRecovered);
                    sqlCmd.Parameters.AddWithValue("@active_cases", countryStats.TotalCases);
                    sqlCmd.Parameters.AddWithValue("@serious_critical", countryStats.SeriousCritical);
                    sqlCmd.Parameters.AddWithValue("@cases_per_mill_pop", countryStats.CasesPerMillPop);


                    sqlConn.Open();
                    using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
                    {
                        sqlAdapter.Fill(dt);
                    }
                }
            }
        }

        private async void sendFirebaseMessage()
        {
            //for Firebase messaging
            //The topic name can be optionally prefixed with "/topics/".
            var topic = "my-test-topic";

            // See documentation on defining a message payload.
            var message = new Message()
            {
                Data = new Dictionary<string, string>()
                {
                    { "score", "850" },
                    { "time", "2:45" },
                },
                Topic = topic,
            };

            //Send a message to the devices subscribed to the provided topic.
            string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            // Response is a message ID string.
            Console.WriteLine("Successfully sent message: " + response);
        }
    }
}
