using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyRSSFeedService.dto;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace MyRSSFeedService
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
                var feeds = new List<EdasRssFeed>();

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Async = true;

                using (WebClient client = new WebClient())
                using (XmlReader reader = XmlReader.Create(client.OpenRead("https://www.edas.info/rssConferences.php"), settings))
                {
                    var aboutAttribute = "";
                    var title = "";
                    var link = "";
                    var description = "";
                    var isTitle = false;
                    var isLink = false;
                    var isDescription = false;


                    while (await reader.ReadAsync())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                {
                                    if (reader.Name.Equals("item", StringComparison.OrdinalIgnoreCase))
                                    {
                                        aboutAttribute = reader.GetAttribute("rdf:about");
                                    }
                                    else if(reader.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isTitle = true;
                                    }
                                    else if (reader.Name.Equals("link", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isLink = true;
                                    }
                                    else if (reader.Name.Equals("description", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isDescription = true;
                                    }
                                    break;
                                }
                            case XmlNodeType.Text:
                                {
                                    if(isTitle)
                                    {
                                        title = await reader.GetValueAsync();
                                        isTitle = false;
                                    }
                                    else if(isLink)
                                    {
                                        link = await reader.GetValueAsync();
                                        isLink = false;
                                    }
                                    else if (isDescription)
                                    {
                                        description = await reader.GetValueAsync();
                                        isDescription = false;
                                    }

                                    break;
                                }
                            case XmlNodeType.EndElement:
                                {
                                    if (reader.Name.Equals("item", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var feed = new EdasRssFeed 
                                        {
                                            AboutURL    = aboutAttribute,
                                            Title       = title,
                                            Link        = link,
                                            Description = description
                                        };

                                        feeds.Add(feed);

                                        aboutAttribute = "";
                                        title = "";
                                        link = "";
                                    }
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }

                _processEdasFeeds(feeds);

                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private void _processEdasFeeds(List<EdasRssFeed> feeds)
        {
            _logger.LogInformation($"Found {feeds.Count} EDAS Feeds. Processing..");

            _publishToTopic(feeds);
        }

        private void _publishToTopic(List<EdasRssFeed> feeds)
        {
            string exchange = "edas-rss-feed";
            string rabbitMQHost = Environment.GetEnvironmentVariable("RABBIT_MQ_HOST");
            var factory = new ConnectionFactory() { HostName = rabbitMQHost };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {

                channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Fanout);

                foreach (EdasRssFeed feed in feeds)
                {
                    var message = JsonConvert.SerializeObject(feed);
                    
                    var body = Encoding.UTF8.GetBytes(message);

                    channel.BasicPublish(exchange: exchange,
                                         routingKey: "",
                                         basicProperties: null,
                                         body: body);

                    _logger.LogInformation($"Send Message {message} to {exchange}.");
                }
               
                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                //var consumer = new EventingBasicConsumer(channel);
                //consumer.Received += (model, ea) =>
                //{
                //    var body = ea.Body.ToArray();
                //    var message = Encoding.UTF8.GetString(body);
                //    _logger.LogInformation(message);

                //    CountryStats countryStats = JsonConvert.DeserializeObject<CountryStats>(message);
                //    _processCoutryStats(countryStats);

                //    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                //};

                //channel.BasicConsume(queue: "covid-19-stats",
                //                     autoAck: false,
                //                     consumer: consumer);
                //_logger.LogInformation("Queue subscribed");


            }
        }
    }
}
