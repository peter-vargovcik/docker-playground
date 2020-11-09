using Newtonsoft.Json;
using System;
using System.Globalization;

namespace WorkerService.converters
{
    class CountryStatsIntConverter : Newtonsoft.Json.JsonConverter
    { 
        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            writer.WriteValue(String.Format("{0:n0}", (int)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            try
            {
                return Int32.Parse((string)reader.Value, NumberStyles.AllowThousands);
            }
            catch (Exception e)
            {

                return -1;
            }
           
            // return Int32.Parse((string)reader.Value.ToString().Replace(",",""));
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}
