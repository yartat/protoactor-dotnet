using System.IO;
using Google.Protobuf;
using Proto.IMDG.PList;
using Wire;

namespace Proto.IMDG
{
    public  static class PSerializer
    {
        private static readonly Serializer Serializer = new Serializer(new SerializerOptions(false, true));
        public static PObject Serialize<T>(T item)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(item, ms);
                ms.Position = 0;
                return new PObject()
                {                
                    Manifest = "",
                    SerializerId = 1,
                    Payload = ByteString.FromStream(ms),
                };
            }
            
        }

        public static object Deserialize(PObject self)
        {
            using (var ms = new MemoryStream(self.Payload.ToByteArray()))
            {
                var res = Serializer.Deserialize(ms);
                return res;
            }
        }

        public static T Deserialize<T>(PObject self)
        {
            using (var ms = new MemoryStream(self.Payload.ToByteArray()))
            {
                var res = Serializer.Deserialize<T>(ms);
                return res;
            }
        }
    }
}