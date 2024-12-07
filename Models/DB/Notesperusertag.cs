using System;
using System.Collections.Generic;

namespace hackwknd_api.Models.DB;

public partial class Notesperusertag
{
    public Guid? Recid { get; set; }

    public Guid? Userrecid { get; set; }

    public string? Tag { get; set; }
    public string Ispublic { get; set; }
}
