using System;
using System.Collections.Generic;

namespace hackwknd_api.Models.DB;

public partial class Chathistory
{
    public Guid Recid { get; set; }

    public Guid Userrecid { get; set; }

    public string Chathistory1 { get; set; } = null!;

    public DateTime? Createdateutc { get; set; }

    public DateTime? Lastupdateutc { get; set; }
}
