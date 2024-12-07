using System;
using System.Collections.Generic;

namespace hackwknd_api.Models.DB;

public partial class Session
{
    public Guid Recid { get; set; }

    public string? Generatedsessionkey { get; set; }

    public DateTime? Createdateutc { get; set; }

    public DateTime? Lastupdateutc { get; set; }

    public Guid? Userrecid { get; set; }
}
