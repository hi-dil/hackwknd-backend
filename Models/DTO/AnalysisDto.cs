namespace hackwknd_api.Models.DTO;

public class AnalysisDto
{
    public List<Answer> questions { get; set; }
    public Score score { get; set; }
}

public class Answer
{
    public string question { get; set; }
    public string explanation { get; set; }
    public bool isCorrect { get; set; }
    public string userAnswer { get; set; }
}

public class Score
{
    public string userScore { get; set; }
    public string totalQuestion { get; set; }
}