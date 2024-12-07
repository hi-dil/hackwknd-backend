using System.Text.Json;
using hackwknd_api.Models.DTO;

namespace hackwknd_api.Models.ExtData;

public class ChatHistoryExtdata
{
    public string type { get; set; }
    public List<ChatLog> logs { get; set; }
    public Guid noteRecid { get; set; }
    public List<AnalysisDto> analysis { get; set; }
    
}