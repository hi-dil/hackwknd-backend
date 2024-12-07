namespace hackwknd_api.Models.DTO;

public class QuestionDto
{
    public string questionID { get; set; }
    public string question { get; set; }
    public string userAnswer { get; set; } = string.Empty;
}