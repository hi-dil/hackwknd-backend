using System;
using System.Collections.Generic;

namespace hackwknd_api.Models.DB;

public partial class User
{
    public Guid Recid { get; set; }

    public string? Name { get; set; }

    public DateTime? Createdateutc { get; set; }

    public DateTime? Lastupdateutc { get; set; }
}
