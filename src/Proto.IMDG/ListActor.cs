using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.IMDG.PList;

namespace Proto.IMDG
{
    public class ListActor : IActor
    {
        public static Props Props => Actor.FromProducer(() => new ListActor());

        private readonly List<object> _list = new List<object>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case AddRequest msg:
                {
                    var obj = PSerializer.Deserialize(msg.Value);
                    _list.Add(obj);
                    break;
                }
                case CountRequest _:
                {
                    context.Respond(new CountResponse {Value = _list.Count});
                    break;
                }
                case ClearRequest _:
                {
                    _list.Clear();

                    break;
                }
                case GetRequest msg:
                {
                    var obj = _list[msg.Index];
                    context.Respond(new GetResponse {Value = PSerializer.Serialize(obj)});
                    break;
                }
            }

            return Actor.Done;
        }
    }
}