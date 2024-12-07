using Microsoft.EntityFrameworkCore;

namespace hackwknd_api.Models.DB;

[Keyless]
public class Pastquiz
{
    public Guid? recid { get; set; }

    public Guid? userrecid { get; set; }

    public string noterecid { get; set; }
    public string analysis { get; set; }
    public DateTime completeddateutc { get; set; }
}