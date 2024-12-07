namespace hackwknd_api.Utils;

public static class Config
{
    public static Creds creds { get; set; } = new();
}

public class Creds
{
    public string openAi { get; set; } 
}
