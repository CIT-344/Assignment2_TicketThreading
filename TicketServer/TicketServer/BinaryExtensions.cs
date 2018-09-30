using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TicketServer.Extensions
{
    public static class BinaryExtensions
    {
        public static void WriteDataModel(this BinaryWriter Writer, Communication_Model Model)
        {
            Writer.Write(JsonConvert.SerializeObject(Model));
        }

        public static Communication_Model ReadDataModel(this BinaryReader Reader)
        {
            var rawData = Reader.ReadString();
            try
            {
                var model = JsonConvert.DeserializeObject<Communication_Model>(rawData);
                return model;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
