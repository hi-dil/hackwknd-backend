namespace hackwknd_api.Models;

public class ChatLog
{
    public string actor { get; set; }
    public string message { get; set; }
    public bool isHidden { get; set; }
}