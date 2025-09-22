namespace Library
{
    public class Message
    {
        public string msg { get; set; }
        public string json { get; set; }
        public Message(string msg, string json)
        {
            this.msg = msg;
            this.json = json;
        }
    }
}
