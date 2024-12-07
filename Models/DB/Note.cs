using System;
using System.Collections.Generic;
using System.Text.Json;

namespace hackwknd_api.Models.DB;

public partial class Note
{
    public Guid Recid { get; set; }

    public string? Datacontent { get; set; }

    public DateTime? Createdateutc { get; set; }

    public DateTime? Lastupdateutc { get; set; }

    public JsonDocument? Extdata { get; set; }

    public Guid? Userrecid { get; set; }
}
